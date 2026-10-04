using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NebuLog.OpenTelemetry;

/// <summary>
/// Owns the one <see cref="NebuLogConnection"/> a process uses, so the exporter, the statistics
/// API and the command API all share a single socket.
/// </summary>
/// <remarks>
/// OpenTelemetry 1.19 exposes no public way to add services from a <c>LoggerProviderBuilder</c>,
/// so <c>AddNebuLogExporter</c> hands its options delegate to this registry instead of to
/// <c>IServiceCollection</c>. The connection itself is created on first use, never at startup.
/// </remarks>
internal sealed class NebuLogClientRegistry : IAsyncDisposable
{
    private readonly List<Action<NebuLogExporterOptions>> _configurations = [];
    private readonly IOptions<NebuLogExporterOptions> _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Lock _gate = new();
    private NebuLogClientIdentity _identity = NebuLogClientIdentity.Unknown;
    private NebuLogConnection? _connection;

    public NebuLogClientRegistry(IOptions<NebuLogExporterOptions> options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
    }

    /// <summary>Registers an additional options delegate, applied when the connection is created.</summary>
    public void AddConfiguration(Action<NebuLogExporterOptions> configure)
    {
        lock (_gate)
        {
            _configurations.Add(configure);
        }
    }

    /// <summary>Records the producer identity read from the OpenTelemetry resource.</summary>
    public void SetIdentity(NebuLogClientIdentity identity)
    {
        lock (_gate)
        {
            if (_connection is not null)
            {
                return;
            }

            _identity = identity;
        }
    }

    /// <summary>Returns the effective options, with every registered delegate applied and validated.</summary>
    public NebuLogExporterOptions BuildOptions()
    {
        var effective = Clone(_options.Value);

        lock (_gate)
        {
            foreach (var configure in _configurations)
            {
                configure(effective);
            }
        }

        Validator.ValidateObject(effective, new ValidationContext(effective), validateAllProperties: true);
        return effective;
    }

    /// <summary>Returns the shared connection, creating it on first use.</summary>
    public INebuLogTransport GetTransport(NebuLogClientIdentity identity)
    {
        lock (_gate)
        {
            if (_connection is null)
            {
                _identity = identity;
                _connection = new NebuLogConnection(BuildOptionsUnlocked(), _identity, _loggerFactory);
            }

            return _connection;
        }
    }

    /// <summary>Returns the shared connection for callers that have no resource identity of their own.</summary>
    public NebuLogConnection GetConnection() => (NebuLogConnection)GetTransport(_identity);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        NebuLogConnection? connection;
        lock (_gate)
        {
            connection = _connection;
            _connection = null;
        }

        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private NebuLogExporterOptions BuildOptionsUnlocked()
    {
        var effective = Clone(_options.Value);
        foreach (var configure in _configurations)
        {
            configure(effective);
        }

        Validator.ValidateObject(effective, new ValidationContext(effective), validateAllProperties: true);
        return effective;
    }

    private static NebuLogExporterOptions Clone(NebuLogExporterOptions source) => new()
    {
        Endpoint = source.Endpoint,
        ApiKey = source.ApiKey,
        QueueCapacity = source.QueueCapacity,
        MaxBatchSize = source.MaxBatchSize,
        FlushInterval = source.FlushInterval,
        ShutdownDrainTimeout = source.ShutdownDrainTimeout,
    };
}
