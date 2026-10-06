namespace NebuLog.Server.Host;

/// <summary>
/// A health probe the container can run as its own command.
/// </summary>
/// <remarks>
/// The ASP.NET runtime image ships neither <c>curl</c> nor <c>wget</c>, so a shell-based Docker
/// HEALTHCHECK silently fails forever and the container is reported unhealthy while the
/// application is perfectly fine. Probing from inside the application needs no extra tools and no
/// extra attack surface.
/// </remarks>
internal static class HealthProbe
{
    /// <summary>The argument that selects probe mode instead of starting the server.</summary>
    public const string Argument = "--health-check";

    /// <summary>Requests the readiness endpoint and reports the result as a process exit code.</summary>
    /// <param name="args">The process arguments; the port may follow the probe argument.</param>
    /// <returns>0 when ready, 1 otherwise.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080";
        var index = Array.IndexOf(args, Argument);
        if (index >= 0 && index + 1 < args.Length)
        {
            port = args[index + 1];
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            using var response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/health/ready"))
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
