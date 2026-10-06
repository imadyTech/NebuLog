namespace NebuLog.OpenTelemetry;

/// <summary>
/// The producer identity advertised to the server, taken from the OpenTelemetry resource.
/// </summary>
/// <param name="ServiceName">The resource's <c>service.name</c>.</param>
/// <param name="ServiceInstanceId">The resource's <c>service.instance.id</c>, when present.</param>
internal sealed record NebuLogClientIdentity(string ServiceName, string? ServiceInstanceId)
{
    /// <summary>The identity used before the OpenTelemetry resource has been resolved.</summary>
    public static NebuLogClientIdentity Unknown { get; } = new("unknown_service", null);
}
