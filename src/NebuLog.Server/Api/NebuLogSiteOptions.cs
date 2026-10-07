namespace NebuLog.Server.Api;

/// <summary>
/// Links the public site shows, bound from configuration section <c>NebuLog:Site</c>.
/// </summary>
/// <remarks>
/// These live in configuration rather than in the frontend bundle so the blog can move without a
/// rebuild and a redeploy of the image.
/// </remarks>
public sealed class NebuLogSiteOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:Site";

    /// <summary>Where the "Blog" link points. Empty hides the link.</summary>
    public string BlogUrl { get; set; } = string.Empty;

    /// <summary>Where the "GitHub" link points.</summary>
    public string GitHubUrl { get; set; } = "https://github.com/imadyTech/NebuLog";
}
