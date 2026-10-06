using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebuLog.Server.Authentication;
using NebuLog.Server.Hubs;
using NebuLog.Server.Identity;
using NebuLog.Server.Infrastructure;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers NebuLog's identity store, authentication schemes and authorisation policies.</summary>
public static class NebuLogSecurityServiceCollectionExtensions
{
    /// <summary>The authentication scheme that picks between API key and cookie per request.</summary>
    public const string SelectorScheme = "NebuLog.Selector";

    /// <summary>The session cookie's name.</summary>
    public const string CookieName = "nebulog.auth";

    /// <summary>
    /// Adds the EF Core identity store, cookie and API-key authentication, and the NebuLog policies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration root; reads <c>ConnectionStrings:Identity</c> and
    /// the <c>NebuLog:Admin</c>, <c>NebuLog:Demo</c>, <c>NebuLog:DemoProducer</c> and
    /// <c>NebuLog:DataProtection</c> sections.</param>
    /// <param name="environment">Used to relax cookie and key-storage settings in development.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddNebuLogSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<NebuLogAdminOptions>().Bind(configuration.GetSection(NebuLogAdminOptions.SectionName));
        services.AddOptions<NebuLogDemoOptions>().Bind(configuration.GetSection(NebuLogDemoOptions.SectionName));
        services.AddOptions<NebuLogDemoProducerOptions>()
            .Bind(configuration.GetSection(NebuLogDemoProducerOptions.SectionName));
        services.AddOptions<NebuLogDataProtectionOptions>()
            .Bind(configuration.GetSection(NebuLogDataProtectionOptions.SectionName));

        AddDbContext(services);

        services.AddIdentityCore<NebuLogUser>(identity =>
            {
                identity.User.RequireUniqueEmail = true;
                identity.SignIn.RequireConfirmedAccount = false;
                identity.Password.RequiredLength = 12;

                // Five failures locks the account for five minutes, per the work order.
                identity.Lockout.AllowedForNewUsers = true;
                identity.Lockout.MaxFailedAccessAttempts = 5;
                identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<NebuLogDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        AddAuthentication(services, environment);
        AddAuthorization(services);
        AddDataProtection(services);

        services.AddMemoryCache();
        services.TryAddSingleton<ApiKeyService>();
        services.AddHostedService<DatabaseInitializer>();

        services.AddHealthChecks()
            .AddDbContextCheck<NebuLogDbContext>(
                name: "identity-database",
                failureStatus: HealthStatus.Unhealthy,
                tags: [IngestQueueHealthCheck.ReadyTag]);

        return services;
    }

    /// <summary>Adds the sign-in rate-limit policy to an existing rate limiter.</summary>
    /// <param name="limiter">The rate-limiter options being configured.</param>
    internal static void AddAuthRateLimitPolicy(RateLimiterOptions limiter) =>
        limiter.AddPolicy(AuthEndpoints.RateLimitPolicy, static context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

    private static void AddDbContext(IServiceCollection services)
    {
        // A host (or a test) may register the context itself, for example to use an open in-memory
        // SQLite connection; in that case leave its registration alone.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(DbContextOptions<NebuLogDbContext>)))
        {
            return;
        }

        // Resolved from the service provider, not read here: configuration sources added after
        // AddNebuLogServer must still be able to set the connection string.
        services.AddDbContext<NebuLogDbContext>((provider, options) =>
            options.UseSqlite(ResolveConnectionString(provider)));
    }

    private static string ResolveConnectionString(IServiceProvider provider)
    {
        var configured = provider.GetRequiredService<IConfiguration>().GetConnectionString("Identity");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        // Development default. Production sets ConnectionStrings__Identity to the mounted volume,
        // because the container's root filesystem is read-only.
        var environment = provider.GetRequiredService<IHostEnvironment>();
        var directory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(directory);

        return $"Data Source={Path.Combine(directory, "nebulog.db")}";
    }

    private static void AddAuthentication(IServiceCollection services, IHostEnvironment environment)
    {
        services.AddAuthentication(SelectorScheme)
            .AddPolicyScheme(SelectorScheme, SelectorScheme, options =>
            {
                // Machines send X-Api-Key; people send the session cookie. Choosing per request
                // keeps a single pipeline instead of splitting the endpoints by audience.
                options.ForwardDefaultSelector = context =>
                    string.IsNullOrEmpty(context.Request.Headers[ApiKeyAuthenticationOptions.HeaderName])
                        ? IdentityConstants.ApplicationScheme
                        : ApiKeyAuthenticationOptions.SchemeName;
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.SchemeName,
                _ => { })
            .AddCookie(IdentityConstants.ApplicationScheme, cookie =>
            {
                cookie.Cookie.Name = CookieName;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Strict;
                cookie.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                cookie.ExpireTimeSpan = TimeSpan.FromHours(8);
                cookie.SlidingExpiration = true;

                // This is an API, not a server-rendered site: answer with status codes rather than
                // redirecting to a login page that does not exist on the server.
                cookie.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                cookie.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
    }

    private static void AddAuthorization(IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(NebuLogPolicies.Viewer, policy => policy.RequireRole(
                NebuLogRoles.Viewer, NebuLogRoles.Operator, NebuLogRoles.Admin))
            .AddPolicy(NebuLogPolicies.Operator, policy => policy.RequireRole(
                NebuLogRoles.Operator, NebuLogRoles.Admin))
            .AddPolicy(NebuLogPolicies.Admin, policy => policy.RequireRole(NebuLogRoles.Admin))
            .AddPolicy(NebuLogPolicies.Producer, policy => policy.RequireRole(NebuLogRoles.Producer));

    private static void AddDataProtection(IServiceCollection services)
    {
        services.AddDataProtection().SetApplicationName("NebuLog");

        // Configured through the options pipeline rather than read at registration time, for the
        // same reason as the connection string above.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<KeyManagementOptions>, ConfigureNebuLogKeyRing>());
    }

    /// <summary>Points the Data Protection key ring at the configured, writable directory.</summary>
    private sealed class ConfigureNebuLogKeyRing : IConfigureOptions<KeyManagementOptions>
    {
        private readonly IOptions<NebuLogDataProtectionOptions> _options;
        private readonly IHostEnvironment _environment;
        private readonly ILoggerFactory _loggerFactory;

        public ConfigureNebuLogKeyRing(
            IOptions<NebuLogDataProtectionOptions> options,
            IHostEnvironment environment,
            ILoggerFactory loggerFactory)
        {
            _options = options;
            _environment = environment;
            _loggerFactory = loggerFactory;
        }

        public void Configure(KeyManagementOptions options)
        {
            var path = _options.Value.KeysPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(_environment.ContentRootPath, "App_Data", "keys");
            }

            // The key ring must live somewhere writable. The container's root filesystem is
            // read-only, so production points this at the mounted volume.
            var directory = Directory.CreateDirectory(path);
            options.XmlRepository = new FileSystemXmlRepository(directory, _loggerFactory);
        }
    }
}
