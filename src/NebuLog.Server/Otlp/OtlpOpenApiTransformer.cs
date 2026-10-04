using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace NebuLog.Server.Otlp;

/// <summary>
/// Declares the two request encodings <c>POST /v1/logs</c> accepts, so they show up in the OpenAPI
/// document and therefore in the Scalar reference page.
/// </summary>
/// <remarks>
/// The endpoint cannot use <c>Accepts&lt;T&gt;()</c> or <c>AcceptsMetadata</c> for this: both turn
/// the content types into a routing constraint, and an unsupported type then falls through to 404
/// instead of the 415 the OTLP specification requires. Documenting the body here keeps the status
/// code correct and the document accurate.
/// </remarks>
internal static class OtlpOpenApiTransformer
{
    public static Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = "An OTLP ExportLogsServiceRequest.",
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                ["application/x-protobuf"] = new(),
                ["application/json"] = new(),
            },
        };

        return Task.CompletedTask;
    }
}
