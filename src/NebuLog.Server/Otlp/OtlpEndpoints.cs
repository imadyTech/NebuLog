using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NebuLog.Contracts;
using NebuLog.Server.Diagnostics;
using NebuLog.Server.Hubs;
using NebuLog.Server.Infrastructure;
using NebuLog.Server.Ingestion;
using NebuLog.Server.Otlp.Json;
using OpenTelemetry.Proto.Collector.Logs.V1;

namespace NebuLog.Server.Otlp;

/// <summary>Maps the OTLP/HTTP log receiver at <c>POST /v1/logs</c>.</summary>
internal static class OtlpEndpoints
{
    private const string ProtobufContentType = "application/x-protobuf";
    private const string JsonContentType = "application/json";

    // google.rpc.Code.INVALID_ARGUMENT
    private const int InvalidArgument = 3;

    public static IEndpointRouteBuilder MapNebuLogOtlp(this IEndpointRouteBuilder endpoints)
    {
        var otlp = endpoints.MapPost("/v1/logs", HandleAsync)
            .WithName("ExportLogs")
            .WithTags("OTLP")
            .WithSummary("OpenTelemetry Protocol log export over HTTP.")
            .WithDescription(
                "Accepts an OTLP ExportLogsServiceRequest as application/x-protobuf or application/json, " +
                "optionally gzip-encoded. Responds with ExportLogsServiceResponse in the same encoding. " +
                "Returns 415 for any other content type, 413 when the body exceeds the configured limit, " +
                "400 for a malformed payload, 429 when rate limited and 503 when the ingest queue is full.")
            // The accepted content types are declared by OtlpOpenApiTransformer rather than with
            // Accepts<T>(...) or AcceptsMetadata: both of those make the content types a routing
            // constraint, so an unsupported type falls through to 404 instead of the 415 the OTLP
            // specification asks for. Verified by UnsupportedContentTypeReturns415.
            .AddOpenApiOperationTransformer(OtlpOpenApiTransformer.TransformAsync)
            .Produces(StatusCodes.Status200OK, contentType: ProtobufContentType)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status413PayloadTooLarge)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .Produces(StatusCodes.Status429TooManyRequests)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting(NebuLogRateLimitOptions.IngestPolicy)
            .RequireCors(NebuLogCorsOptions.PolicyName);

        return endpoints;
    }

    /// <summary>Policy: <see cref="NebuLogPolicies.Producer"/> once WO-0005 enables authentication.</summary>
    private static async Task HandleAsync(
        HttpContext context,
        ILogIngestor ingestor,
        TimeProvider timeProvider,
        IOptions<NebuLogOtlpOptions> otlpOptions)
    {
        var wantsJson = IsJson(context.Request.ContentType);
        if (!wantsJson && !IsProtobuf(context.Request.ContentType))
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        using var activity = ServerMetrics.ActivitySource.StartActivity("nebulog.otlp.export", ActivityKind.Server);
        activity?.SetTag("nebulog.otlp.encoding", wantsJson ? "json" : "protobuf");

        var limit = otlpOptions.Value.MaxRequestBodyBytes;
        byte[] body;
        try
        {
            body = await ReadBodyAsync(context, limit).ConfigureAwait(false);
        }
        catch (BodyTooLargeException)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        OtlpPayload payload;
        try
        {
            payload = wantsJson ? DecodeJson(body) : DecodeProtobuf(body);
        }
        catch (Exception exception) when (exception is InvalidProtocolBufferException or JsonException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            await WriteBadRequestAsync(context, wantsJson, exception.Message).ConfigureAwait(false);
            return;
        }

        var entries = OtlpLogMapper.Map(payload, timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        activity?.SetTag("nebulog.otlp.records", entries.Count);

        var result = ingestor.TryIngest(entries, IngestSource.Otlp);

        if (result.Accepted == 0 && result.Rejected > 0)
        {
            // OTLP treats 503 as retryable, which is exactly the right signal for a full queue.
            context.Response.Headers.RetryAfter = "1";
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            activity?.SetStatus(ActivityStatusCode.Error, "ingest queue full");
            return;
        }

        await WriteSuccessAsync(context, wantsJson, result.Rejected).ConfigureAwait(false);
    }

    private static OtlpPayload DecodeProtobuf(byte[] body) =>
        OtlpProtobufDecoder.Decode(ExportLogsServiceRequest.Parser.ParseFrom(body));

    private static OtlpPayload DecodeJson(byte[] body)
    {
        var request = JsonSerializer.Deserialize(body, OtlpJsonContext.Default.OtlpJsonExportRequest)
            ?? throw new JsonException("The request body was empty or null.");

        return OtlpJsonDecoder.Decode(request);
    }

    private static async Task WriteSuccessAsync(HttpContext context, bool wantsJson, int rejected)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;

        if (wantsJson)
        {
            context.Response.ContentType = JsonContentType;
            var response = new OtlpJsonExportResponse
            {
                PartialSuccess = rejected == 0
                    ? null
                    : new OtlpJsonPartialSuccess
                    {
                        RejectedLogRecords = rejected,
                        ErrorMessage = "ingest queue full",
                    },
            };

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                response,
                OtlpJsonContext.Default.OtlpJsonExportResponse,
                context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var proto = new ExportLogsServiceResponse();
        if (rejected > 0)
        {
            proto.PartialSuccess = new ExportLogsPartialSuccess
            {
                RejectedLogRecords = rejected,
                ErrorMessage = "ingest queue full",
            };
        }

        context.Response.ContentType = ProtobufContentType;
        await context.Response.Body.WriteAsync(proto.ToByteArray(), context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task WriteBadRequestAsync(HttpContext context, bool wantsJson, string message)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        if (wantsJson)
        {
            context.Response.ContentType = JsonContentType;
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new OtlpJsonStatus { Code = InvalidArgument, Message = message },
                OtlpJsonContext.Default.OtlpJsonStatus,
                context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var status = new Google.Rpc.Status { Code = InvalidArgument, Message = message };
        context.Response.ContentType = ProtobufContentType;
        await context.Response.Body.WriteAsync(status.ToByteArray(), context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadBodyAsync(HttpContext context, int limit)
    {
        if (context.Request.ContentLength > limit)
        {
            throw new BodyTooLargeException();
        }

        await using var source = IsGzip(context.Request.Headers.ContentEncoding)
            ? new GZipStream(context.Request.Body, CompressionMode.Decompress)
            : (Stream)context.Request.Body;

        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;

        // Decompressed payloads can exceed Content-Length, so the limit is enforced while reading.
        while ((read = await source.ReadAsync(chunk, context.RequestAborted).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                throw new BodyTooLargeException();
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static bool IsGzip(IEnumerable<string?> contentEncoding) =>
        contentEncoding.Any(value => string.Equals(value, "gzip", StringComparison.OrdinalIgnoreCase));

    private static bool IsProtobuf(string? contentType) =>
        contentType?.StartsWith(ProtobufContentType, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsJson(string? contentType) =>
        contentType?.StartsWith(JsonContentType, StringComparison.OrdinalIgnoreCase) == true;

    private sealed class BodyTooLargeException : Exception;
}
