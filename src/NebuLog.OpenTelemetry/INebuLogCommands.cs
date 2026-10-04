using NebuLog.Contracts;

namespace NebuLog.OpenTelemetry;

/// <summary>Receives commands sent from a NebuLog dashboard to this process.</summary>
public interface INebuLogCommands
{
    /// <summary>Raised on a thread-pool thread whenever the server delivers a command.</summary>
    event EventHandler<NebuLogCommand>? CommandReceived;
}
