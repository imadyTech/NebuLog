using System.Diagnostics;

namespace NebuShop.Orders;

/// <summary>
/// Writes the current activity's W3C trace context onto an outbound request.
/// </summary>
/// <remarks>
/// On a normal socket-backed <see cref="HttpClient"/>, .NET already does this inside
/// <c>SocketsHttpHandler</c>, so adding it looks redundant. It is not: propagation then depends on
/// the transport rather than on the application, and it silently disappears whenever the primary
/// handler is replaced — which is exactly what the integration tests do when they point the shop at
/// an in-memory payments service.
/// <para>
/// Scenario 02 claims both services share one TraceId. Making the injection explicit means the
/// claim is tested by the same code path that serves it, instead of by a mechanism the test harness
/// quietly removes. The header is left alone when the caller has already set one.
/// </para>
/// </remarks>
public sealed class TraceContextPropagationHandler : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var activity = Activity.Current;

        if (activity is not null && !request.Headers.Contains("traceparent"))
        {
            DistributedContextPropagator.Current.Inject(activity, request, static (carrier, name, value) =>
            {
                if (carrier is HttpRequestMessage message)
                {
                    message.Headers.TryAddWithoutValidation(name, value);
                }
            });
        }

        return base.SendAsync(request, cancellationToken);
    }
}
