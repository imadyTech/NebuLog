using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace NebuLog.Server.Tests;

/// <summary>
/// Adds a route that always throws, so the exception handler can be exercised end to end.
/// </summary>
/// <remarks>
/// Registered after NebuLog's own startup filter, which places this middleware inside
/// <c>UseExceptionHandler</c> — exactly where an application's own buggy endpoint would sit.
/// </remarks>
internal sealed class ThrowingEndpointStartupFilter : IStartupFilter
{
    /// <summary>The path that throws.</summary>
    public const string Path = "/test-throw";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            app.Map(Path, branch => branch.Run(
                _ => throw new InvalidOperationException("deliberate failure from a test endpoint")));

            next(app);
        };
    }
}
