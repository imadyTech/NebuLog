using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace NebuLog.Server.Identity;

/// <summary>Issues, revokes and verifies API keys.</summary>
public sealed class ApiKeyService
{
    /// <summary>How long a key lookup stays cached. Revocation evicts the entry immediately.</summary>
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);

    /// <summary>How often <see cref="ApiKey.LastUsedAt"/> is written back for a busy key.</summary>
    public static readonly TimeSpan LastUsedWriteInterval = TimeSpan.FromMinutes(1);

    private const string CacheKeyPrefix = "nebulog.apikey.";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the service.</summary>
    /// <param name="scopeFactory">Used to open a short-lived scope per lookup, since authentication
    /// happens outside any request scope that owns a <see cref="NebuLogDbContext"/>.</param>
    /// <param name="cache">Cache for prefix lookups.</param>
    /// <param name="timeProvider">Clock.</param>
    public ApiKeyService(IServiceScopeFactory scopeFactory, IMemoryCache cache, TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _timeProvider = timeProvider;
    }

    /// <summary>Verifies a presented key.</summary>
    /// <param name="presented">The value of the <c>X-Api-Key</c> header.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The matching active key, or <see langword="null"/> when the key is unknown,
    /// malformed or revoked.</returns>
    public async Task<ApiKeySnapshot?> VerifyAsync(string? presented, CancellationToken cancellationToken)
    {
        if (!ApiKeyGenerator.TryParse(presented, out var parsed))
        {
            return null;
        }

        var snapshot = await GetByPrefixAsync(parsed.Prefix, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || !ApiKeyGenerator.SecretMatches(parsed.Secret, snapshot.SecretHash))
        {
            return null;
        }

        await TouchAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    /// <summary>Issues a new key.</summary>
    /// <param name="name">Human-readable name.</param>
    /// <param name="serviceName">Optional service the key belongs to.</param>
    /// <param name="createdBy">Who is issuing it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored record and the clear text, which is never recoverable afterwards.</returns>
    public async Task<(ApiKey Key, string ClearText)> CreateAsync(
        string name,
        string? serviceName,
        string createdBy,
        CancellationToken cancellationToken)
    {
        var generated = ApiKeyGenerator.Generate();
        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            Name = name,
            Prefix = generated.Prefix,
            SecretHash = generated.SecretHash,
            ServiceName = serviceName,
            CreatedAt = _timeProvider.GetUtcNow(),
            CreatedBy = createdBy,
        };

        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<NebuLogDbContext>();
        database.ApiKeys.Add(key);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _cache.Remove(CacheKeyPrefix + key.Prefix);
        return (key, generated.ClearText);
    }

    /// <summary>Revokes a key and evicts it from the cache so it stops working at once.</summary>
    /// <param name="id">The key's id.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when a key was revoked.</returns>
    public async Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<NebuLogDbContext>();

        var key = await database.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, cancellationToken).ConfigureAwait(false);
        if (key is null || key.RevokedAt is not null)
        {
            return false;
        }

        key.RevokedAt = _timeProvider.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _cache.Remove(CacheKeyPrefix + key.Prefix);
        return true;
    }

    /// <summary>Lists every key, newest first, without any secret material.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<NebuLogDbContext>();

        // SQLite cannot ORDER BY a DateTimeOffset, and the key list is small enough that sorting
        // after materialising costs nothing.
        var keys = await database.ApiKeys
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. keys.OrderByDescending(key => key.CreatedAt)];
    }

    private async Task<ApiKeySnapshot?> GetByPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeyPrefix + prefix;
        if (_cache.TryGetValue<ApiKeySnapshot?>(cacheKey, out var cached))
        {
            return cached;
        }

        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<NebuLogDbContext>();

        var key = await database.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Prefix == prefix && k.RevokedAt == null, cancellationToken)
            .ConfigureAwait(false);

        var snapshot = key is null
            ? null
            : new ApiKeySnapshot(key.Id, key.Name, key.Prefix, key.SecretHash, key.ServiceName, key.LastUsedAt);

        // A miss is cached too, so an invalid key cannot be used to hammer the database.
        _cache.Set(cacheKey, snapshot, CacheLifetime);
        return snapshot;
    }

    /// <summary>
    /// Records that a key was used, at most once per <see cref="LastUsedWriteInterval"/>, without
    /// making the authenticating request wait for the write.
    /// </summary>
    private async Task TouchAsync(ApiKeySnapshot snapshot, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (snapshot.LastUsedAt is { } last && now - last < LastUsedWriteInterval)
        {
            return;
        }

        snapshot.LastUsedAt = now;

        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<NebuLogDbContext>();

        await database.ApiKeys
            .Where(key => key.Id == snapshot.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(key => key.LastUsedAt, now), cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>The cached view of an active key.</summary>
/// <param name="Id">The key's id.</param>
/// <param name="Name">The key's name.</param>
/// <param name="Prefix">The lookup prefix.</param>
/// <param name="SecretHash">The stored hash.</param>
/// <param name="ServiceName">The pinned service name, if any.</param>
/// <param name="LastUsedAt">When the key was last seen, as of the moment it was cached.</param>
public sealed record ApiKeySnapshot(
    Guid Id,
    string Name,
    string Prefix,
    byte[] SecretHash,
    string? ServiceName,
    DateTimeOffset? LastUsedAt)
{
    /// <summary>When the key was last seen; updated in place so the cache throttles writes.</summary>
    public DateTimeOffset? LastUsedAt { get; set; } = LastUsedAt;
}
