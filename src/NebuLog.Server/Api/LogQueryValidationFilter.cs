using Microsoft.AspNetCore.Http;
using NebuLog.Contracts;

namespace NebuLog.Server.Api;

/// <summary>Rejects out-of-range query parameters on <c>GET /api/logs</c> with a validation problem.</summary>
internal sealed class LogQueryValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var query = context.HttpContext.Request.Query;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (TryReadInt(query, "limit", errors) is { } limit &&
            (limit < 1 || limit > HistoryQuery.MaxLimit))
        {
            errors["limit"] = [$"limit must be between 1 and {HistoryQuery.MaxLimit}."];
        }

        if (TryReadInt(query, "minSeverity", errors) is { } minSeverity &&
            (minSeverity < 0 || minSeverity > 24))
        {
            errors["minSeverity"] = ["minSeverity must be between 0 and 24."];
        }

        if (TryReadLong(query, "afterId", errors) is { } afterId && afterId < 0)
        {
            errors["afterId"] = ["afterId cannot be negative."];
        }

        return errors.Count > 0
            ? TypedResults.ValidationProblem(errors)
            : await next(context).ConfigureAwait(false);
    }

    private static int? TryReadInt(IQueryCollection query, string key, Dictionary<string, string[]> errors)
    {
        var raw = query[key].ToString();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        if (int.TryParse(raw, out var value))
        {
            return value;
        }

        errors[key] = [$"{key} must be an integer."];
        return null;
    }

    private static long? TryReadLong(IQueryCollection query, string key, Dictionary<string, string[]> errors)
    {
        var raw = query[key].ToString();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        if (long.TryParse(raw, out var value))
        {
            return value;
        }

        errors[key] = [$"{key} must be an integer."];
        return null;
    }
}
