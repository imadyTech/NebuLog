namespace NebuLog.Server.Identity;

/// <summary>The initial administrator, bound from configuration section <c>NebuLog:Admin</c>.</summary>
/// <remarks>
/// Matches the deployment keys agreed in ENV-REQ-001: <c>NebuLog__Admin__Email</c> and
/// <c>NebuLog__Admin__Password</c>. The account is created only when it does not already exist, so
/// restarting the container never resets a password that was changed afterwards.
/// </remarks>
public sealed class NebuLogAdminOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:Admin";

    /// <summary>The administrator's email address; seeding is skipped when empty.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The administrator's initial password; seeding is skipped when empty.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>The public read-only demo account, bound from <c>NebuLog:Demo</c>.</summary>
public sealed class NebuLogDemoOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:Demo";

    /// <summary>The fixed address of the demo account.</summary>
    public const string DemoEmail = "demo@nebulog.local";

    /// <summary>Whether visitors may sign in as the demo user.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>The demo producer's API key, bound from <c>NebuLog:DemoProducer</c>.</summary>
/// <remarks>Matches the deployment key <c>NebuLog__DemoProducer__ApiKey</c> from ENV-REQ-001.</remarks>
public sealed class NebuLogDemoProducerOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:DemoProducer";

    /// <summary>The name given to the seeded key.</summary>
    public const string KeyName = "demo-producer";

    /// <summary>The clear-text key to accept; only its hash is stored. Empty disables seeding.</summary>
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>Where Data Protection keys live, bound from <c>NebuLog:DataProtection</c>.</summary>
public sealed class NebuLogDataProtectionOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:DataProtection";

    /// <summary>
    /// Directory for the key ring. Must be writable: the container runs with a read-only root
    /// filesystem, so in production this has to be under the mounted volume.
    /// </summary>
    public string KeysPath { get; set; } = string.Empty;
}
