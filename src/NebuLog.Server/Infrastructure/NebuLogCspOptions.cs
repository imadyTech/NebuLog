namespace NebuLog.Server.Infrastructure;

/// <summary>
/// Extra content-security-policy sources, bound from configuration section
/// <c>NebuLog:Security:Csp</c>.
/// </summary>
/// <remarks>
/// The default policy allows nothing but this origin, which is the right default and is what makes
/// the dashboard safe to show untrusted log content. A deployment sometimes has to admit one more
/// origin anyway — the public demo is fronted by Cloudflare, whose analytics beacon the edge injects
/// into the page and which the policy would otherwise block (ENV-REQ-002 follow-up).
/// <para>
/// These are configuration, not secrets, and they are additive only: nothing here can remove a
/// directive or widen one that is not listed. Leaving them empty keeps the original policy exactly.
/// </para>
/// </remarks>
public sealed class NebuLogCspOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:Security:Csp";

    /// <summary>Extra <c>script-src</c> sources, space-separated. Empty adds nothing.</summary>
    public string ExtraScriptSrc { get; set; } = string.Empty;

    /// <summary>Extra <c>connect-src</c> sources, space-separated. Empty adds nothing.</summary>
    public string ExtraConnectSrc { get; set; } = string.Empty;
}
