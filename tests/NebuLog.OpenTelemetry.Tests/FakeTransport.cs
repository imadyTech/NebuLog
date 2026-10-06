using NebuLog.Contracts;
using NebuLog.OpenTelemetry;

namespace NebuLog.OpenTelemetry.Tests;

/// <summary>Captures published batches, optionally blocking until the test releases it.</summary>
internal sealed class FakeTransport : INebuLogTransport
{
    private readonly List<NebuLogEntry> _entries = [];
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource _received = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource? Gate { get; init; }

    public Task FirstBatchReceived => _received.Task;

    public IReadOnlyList<NebuLogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public async Task PublishAsync(IReadOnlyList<NebuLogEntry> entries, CancellationToken cancellationToken)
    {
        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _entries.AddRange(entries);
        }

        _received.TrySetResult();
    }
}
