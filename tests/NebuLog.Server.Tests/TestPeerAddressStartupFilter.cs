using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace NebuLog.Server.Tests;

/// <summary>
/// Gives in-memory requests a peer address, which <c>TestServer</c> otherwise leaves null.
/// </summary>
/// <remarks>
/// Without this the forwarded-headers middleware has no address to compare against the trusted
/// networks, so a test could not tell a trusted proxy from an untrusted one. The address comes from
/// the <c>X-Test-Peer</c> request header, defaulting to a loopback address.
/// </remarks>
internal sealed class TestPeerAddressStartupFilter : IStartupFilter
{
    /// <summary>Header the test uses to choose the peer address for one request.</summary>
    public const string HeaderName = "X-Test-Peer";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            app.Use(async (context, following) =>
            {
                var raw = context.Request.Headers[HeaderName].ToString();
                context.Connection.RemoteIpAddress = IPAddress.TryParse(raw, out var address)
                    ? address
                    : IPAddress.Loopback;

                await following().ConfigureAwait(false);
            });

            next(app);
        };
    }
}
