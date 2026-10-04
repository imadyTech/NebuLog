using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NebuLog.Server.Identity;

/// <summary>
/// Applies pending migrations and seeds roles, the administrator, the demo user and the demo
/// producer key at startup.
/// </summary>
/// <remarks>
/// Migrating from inside the application is a deliberate trade-off for this deployment: a single
/// SQLite file on one container, where a separate migration step would add operational work for no
/// benefit. It would be the wrong choice for a multi-instance deployment, where two instances could
/// race; that trade-off is recorded in the README.
/// All seeding is idempotent — an existing account is never modified, so a restart cannot reset a
/// password that was changed afterwards.
/// </remarks>
internal sealed partial class DatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IServiceScopeFactory scopeFactory, ILogger<DatabaseInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<NebuLogDbContext>();

        if (database.Database.IsRelational())
        {
            await database.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        await SeedRolesAsync(services, cancellationToken).ConfigureAwait(false);
        await SeedAdminAsync(services).ConfigureAwait(false);
        await SeedDemoUserAsync(services).ConfigureAwait(false);
        await SeedDemoProducerAsync(services, database, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedRolesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in NebuLogRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await roles.RoleExistsAsync(role).ConfigureAwait(false))
            {
                await roles.CreateAsync(new IdentityRole(role)).ConfigureAwait(false);
            }
        }
    }

    private async Task SeedAdminAsync(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<NebuLogAdminOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            AdminNotConfigured();
            return;
        }

        await EnsureUserAsync(services, options.Email, options.Password, NebuLogRoles.Admin).ConfigureAwait(false);
    }

    private async Task SeedDemoUserAsync(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<NebuLogDemoOptions>>().Value;
        if (!options.Enabled)
        {
            return;
        }

        // The demo account is sign-in-by-button only; the password is never published, and the
        // account holds nothing but the read-only Viewer role.
        await EnsureUserAsync(
            services,
            NebuLogDemoOptions.DemoEmail,
            ApiKeyGenerator.Generate().ClearText,
            NebuLogRoles.Viewer).ConfigureAwait(false);
    }

    private async Task EnsureUserAsync(IServiceProvider services, string email, string password, string role)
    {
        var users = services.GetRequiredService<UserManager<NebuLogUser>>();

        var existing = await users.FindByEmailAsync(email).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, role).ConfigureAwait(false))
            {
                await users.AddToRoleAsync(existing, role).ConfigureAwait(false);
            }

            return;
        }

        var user = new NebuLogUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await users.CreateAsync(user, password).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            SeedUserFailed(email, string.Join("; ", created.Errors.Select(error => error.Description)));
            return;
        }

        await users.AddToRoleAsync(user, role).ConfigureAwait(false);
        SeededUser(email, role);
    }

    private async Task SeedDemoProducerAsync(
        IServiceProvider services,
        NebuLogDbContext database,
        CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<IOptions<NebuLogDemoProducerOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return;
        }

        if (!ApiKeyGenerator.TryParse(options.ApiKey, out var parsed))
        {
            DemoProducerKeyMalformed();
            return;
        }

        var existing = await database.ApiKeys
            .FirstOrDefaultAsync(key => key.Prefix == parsed.Prefix, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return;
        }

        database.ApiKeys.Add(new ApiKey
        {
            Id = Guid.NewGuid(),
            Name = NebuLogDemoProducerOptions.KeyName,
            Prefix = parsed.Prefix,
            SecretHash = ApiKeyGenerator.HashSecret(parsed.Secret),
            ServiceName = null,
            CreatedAt = services.GetRequiredService<TimeProvider>().GetUtcNow(),
            CreatedBy = "seed",
        });

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        SeededDemoProducer();
    }

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Warning,
        Message = "No administrator is configured; set NebuLog:Admin:Email and NebuLog:Admin:Password.")]
    private partial void AdminNotConfigured();

    [LoggerMessage(EventId = 1202, Level = LogLevel.Information, Message = "Seeded user {Email} in role {Role}.")]
    private partial void SeededUser(string email, string role);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Error, Message = "Could not seed user {Email}: {Errors}")]
    private partial void SeedUserFailed(string email, string errors);

    [LoggerMessage(
        EventId = 1204,
        Level = LogLevel.Warning,
        Message = "NebuLog:DemoProducer:ApiKey is not a well-formed NebuLog key; it was ignored.")]
    private partial void DemoProducerKeyMalformed();

    [LoggerMessage(EventId = 1205, Level = LogLevel.Information, Message = "Seeded the demo producer API key.")]
    private partial void SeededDemoProducer();
}
