using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NebuShop.Orders;

/// <summary>
/// An action filter that logs the action it wraps and how long it took.
/// </summary>
/// <remarks>
/// Present so the MVC pipeline has at least one filter of its own in the console: filters are a
/// visible part of what the MVC pipeline does that the Minimal API pipeline does not.
/// </remarks>
public sealed class ShopActionFilter : IAsyncActionFilter
{
    private readonly ILogger<ShopActionFilter> _logger;

    /// <summary>Creates the filter.</summary>
    /// <param name="logger">Where the filter's entries go.</param>
    public ShopActionFilter(ILogger<ShopActionFilter> logger) => _logger = logger;

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var action = context.ActionDescriptor.DisplayName ?? "unknown";
        FilterLog.ActionStarting(_logger, action, context.ActionArguments.Count);

        var started = Stopwatch.GetTimestamp();
        var executed = await next().ConfigureAwait(false);
        var elapsed = Stopwatch.GetElapsedTime(started);

        FilterLog.ActionFinished(
            _logger,
            action,
            (int)elapsed.TotalMilliseconds,
            executed.Exception is null ? "ok" : "faulted");
    }
}

internal static partial class FilterLog
{
    [LoggerMessage(EventId = 30, Level = LogLevel.Debug,
        Message = "MVC filter: entering {Action} with {ArgumentCount} bound arguments")]
    public static partial void ActionStarting(ILogger logger, string action, int argumentCount);

    [LoggerMessage(EventId = 31, Level = LogLevel.Debug,
        Message = "MVC filter: {Action} finished {Outcome} in {ElapsedMs} ms")]
    public static partial void ActionFinished(ILogger logger, string action, int elapsedMs, string outcome);
}
