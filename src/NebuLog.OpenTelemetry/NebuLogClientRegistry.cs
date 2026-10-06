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
    private Func<NebuLogClientIdentity>? _identitySource;
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

    /// <summary>
    /// Registers a way to read the producer identity from the OpenTelemetry resource on demand.
    /// </summary>
    /// <remarks>
    /// The identity has to be resolved when the connection opens, not when the exporter is built:
    /// whichever of the exporter, <see cref="INebuLogStats"/> or <see cref="INebuLogCommands"/>
    /// touches the connection first decides the service name sent on the query string. A process
    /// that declares a statistic before writing its first log would otherwise register as
    /// "unknown_service".
    /// </remarks>
    /// <param name="source">Returns the identity; called at most once, when the connection opens.</param>
    public void SetIdentitySource(Func<NebuLogClientIdentity> source)
    {
        lock (_gate)
        {
            _identitySource ??= source;
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
                _identity = Resolve(identity);
                _connection = new NebuLogConnection(BuildOptionsUnlocked(), _identity, _loggerFactory);
            }

            return _connection;
        }
    }

    /// <summary>Returns the shared connection for callers that have no resource identity of their own.</summary>
    public NebuLogConnection GetConnection() => (NebuLogConnection)GetTransport(_identity);

    /// <summary>The identity the connection was opened with. Exposed for tests.</summary>
    internal NebuLogClientIdentity ResolvedIdentity
    {
        get
        {
            lock (_gate)
            {
                return _identity;
            }
        }
    }

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

    /// <summary>
    /// Prefers a real identity over the placeholder, asking the resource only when the caller
    /// could not supply one.
    /// </summary>
    private NebuLogClientIdentity Resolve(NebuLogClientIdentity supplied)
    {
        if (supplied.ServiceName != NebuLogClientIdentity.Unknown.ServiceName)
        {
            return supplied;
        }

        var fromResource = _identitySource?.Invoke();
        return fromResource is not null && fromResource.ServiceName != NebuLogClientIdentity.Unknown.ServiceName
            ? fromResource
            : supplied;
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
