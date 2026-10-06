using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NebuLog.Server.Infrastructure;

/// <summary>
/// Turns unhandled exceptions into RFC 9457 problem details. Outside Development the response
/// carries only a trace id, never the exception type or stack.
/// </summary>
internal sealed partial class NebuLogExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<NebuLogExceptionHandler> _logger;

    public NebuLogExceptionHandler(
        IProblemDetailsService problemDetails,
        IHostEnvironment environment,
        ILogger<NebuLogExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        UnhandledException(exception, traceId, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
                Detail = _environment.IsDevelopment() ? exception.ToString() : null,
                Extensions = { ["traceId"] = traceId },
            },
        }).ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {Path} (traceId {TraceId}).")]
    private partial void UnhandledException(Exception exception, string traceId, string path);
}
