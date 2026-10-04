namespace NebuLog.Server.Identity;

/// <summary>A credential a log producer authenticates with. Only the hash of the secret is stored.</summary>
public sealed class ApiKey
{
    /// <summary>Surrogate key.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable name, shown in the dashboard.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The key's public 8-character prefix, used to look the record up.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>SHA-256 of the secret half of the key.</summary>
    public byte[] SecretHash { get; set; } = [];

    /// <summary>The <c>service.name</c> this key is expected to report as, when pinned.</summary>
    public string? ServiceName { get; set; }

    /// <summary>When the key was issued.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the key was revoked, or <see langword="null"/> while it is active.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>When the key last authenticated a request; updated at most once a minute.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Who issued the key.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>True while the key may still be used.</summary>
    public bool IsActive => RevokedAt is null;
}
