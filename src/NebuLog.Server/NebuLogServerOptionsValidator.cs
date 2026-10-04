using Microsoft.Extensions.Options;

namespace NebuLog.Server;

/// <summary>
/// Checks the constraints that DataAnnotations cannot express: the broadcast interval's bounds and
/// the relationship between the ingest queue and the ring buffer.
/// </summary>
internal sealed class NebuLogServerOptionsValidator : IValidateOptions<NebuLogServerOptions>
{
    private static readonly TimeSpan MinBroadcastInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan MaxBroadcastInterval = TimeSpan.FromSeconds(2);

    public ValidateOptionsResult Validate(string? name, NebuLogServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.BroadcastInterval < MinBroadcastInterval || options.BroadcastInterval > MaxBroadcastInterval)
        {
            failures.Add(
                $"{nameof(NebuLogServerOptions.BroadcastInterval)} must be between " +
                $"{MinBroadcastInterval.TotalMilliseconds:0} ms and {MaxBroadcastInterval.TotalSeconds:0} s, " +
                $"but was {options.BroadcastInterval}.");
        }

        if (options.MaxBroadcastBatch > options.BufferCapacity)
        {
            failures.Add(
                $"{nameof(NebuLogServerOptions.MaxBroadcastBatch)} ({options.MaxBroadcastBatch}) cannot exceed " +
                $"{nameof(NebuLogServerOptions.BufferCapacity)} ({options.BufferCapacity}).");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
