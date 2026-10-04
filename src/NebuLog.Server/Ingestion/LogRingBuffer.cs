using Microsoft.Extensions.Options;
using NebuLog.Contracts;

namespace NebuLog.Server.Ingestion;

/// <summary>
/// A fixed-capacity, thread-safe ring of the most recent entries. Once full, each new entry
/// overwrites the oldest one.
/// </summary>
/// <remarks>
/// Synchronisation is a <see cref="ReaderWriterLockSlim"/> rather than a lock-free snapshot ring.
/// There is exactly one writer (the pipeline's background service) and readers are either
/// dashboards replaying history or REST queries — both of which copy out whole result sets.
/// A reader-writer lock keeps those copies consistent for the cost of one uncontended lock per
/// append, which is far cheaper than the alternative of allocating an immutable snapshot per write.
/// </remarks>
public sealed class LogRingBuffer : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly NebuLogEntry[] _entries;
    private int _start;
    private int _count;
    private bool _disposed;

    /// <summary>Creates a buffer sized from <see cref="NebuLogServerOptions.BufferCapacity"/>.</summary>
    /// <param name="options">The server options.</param>
    public LogRingBuffer(IOptions<NebuLogServerOptions> options)
        : this(options?.Value.BufferCapacity ?? throw new ArgumentNullException(nameof(options)))
    {
    }

    /// <summary>Creates a buffer with an explicit capacity.</summary>
    /// <param name="capacity">The number of entries to retain; must be positive.</param>
    public LogRingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        Capacity = capacity;
        _entries = new NebuLogEntry[capacity];
    }

    /// <summary>The number of entries the buffer retains.</summary>
    public int Capacity { get; }

    /// <summary>The number of entries currently held.</summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>Appends entries, overwriting the oldest once the buffer is full.</summary>
    /// <param name="entries">The entries to append, in ascending id order.</param>
    public void Append(IReadOnlyList<NebuLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return;
        }

        _lock.EnterWriteLock();
        try
        {
            foreach (var entry in entries)
            {
                var index = (_start + _count) % Capacity;
                _entries[index] = entry;

                if (_count == Capacity)
                {
                    _start = (_start + 1) % Capacity;
                }
                else
                {
                    _count++;
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>Returns every retained entry, oldest first.</summary>
    public IReadOnlyList<NebuLogEntry> Snapshot()
    {
        _lock.EnterReadLock();
        try
        {
            return CopyUnlocked();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>Returns the retained entries matching a query, oldest first.</summary>
    /// <param name="query">The filter to apply; <see cref="HistoryQuery.Limit"/> is clamped to
    /// <see cref="HistoryQuery.MaxLimit"/>.</param>
    /// <param name="search">Optional case-insensitive substring the entry body must contain.</param>
    public IReadOnlyList<NebuLogEntry> Query(HistoryQuery query, string? search = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, HistoryQuery.MaxLimit);
        var results = new List<NebuLogEntry>();

        _lock.EnterReadLock();
        try
        {
            for (var i = 0; i < _count && results.Count < limit; i++)
            {
                var entry = _entries[(_start + i) % Capacity];

                if (query.AfterId is { } afterId && entry.Id <= afterId)
                {
                    continue;
                }

                if (query.MinSeverity is { } minSeverity && entry.SeverityNumber < minSeverity)
                {
                    continue;
                }

                if (query.Service is { Length: > 0 } service &&
                    !string.Equals(entry.ServiceName, service, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (search is { Length: > 0 } &&
                    !entry.Body.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(entry);
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        return results;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lock.Dispose();
    }

    private NebuLogEntry[] CopyUnlocked()
    {
        var copy = new NebuLogEntry[_count];
        for (var i = 0; i < _count; i++)
        {
            copy[i] = _entries[(_start + i) % Capacity];
        }

        return copy;
    }
}
