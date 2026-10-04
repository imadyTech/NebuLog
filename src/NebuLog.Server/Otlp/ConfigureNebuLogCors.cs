using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace NebuLog.Server.Otlp;

/// <summary>Builds the <c>otlp-browser</c> CORS policy from <see cref="NebuLogCorsOptions"/>.</summary>
/// <remarks>
/// Configured through <see cref="IConfigureOptions{TOptions}"/> rather than read straight from
/// <c>IConfiguration</c> at registration time, so the policy sees configuration sources that are
/// added after <c>AddNebuLogServer</c> runs — which is exactly what a test host does.
/// </remarks>
internal sealed class ConfigureNebuLogCors : IConfigureOptions<CorsOptions>
{
    private readonly IOptions<NebuLogCorsOptions> _options;

    public ConfigureNebuLogCors(IOptions<NebuLogCorsOptions> options) => _options = options;

    public void Configure(CorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var origins = _options.Value.ParseOrigins();

        options.AddPolicy(NebuLogCorsOptions.PolicyName, policy =>
        {
            // With no origins configured the policy matches nothing, which is the safe default for
            // an endpoint that accepts writes.
            policy.WithOrigins(origins)
                .WithMethods("POST")
                .WithHeaders("Content-Type", "X-Api-Key", "Content-Encoding");
        });
    }
}
