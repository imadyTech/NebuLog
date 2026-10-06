using Microsoft.AspNetCore.Identity;

namespace NebuLog.Server.Identity;

/// <summary>A dashboard user. Plain ASP.NET Core Identity; NebuLog adds no profile fields yet.</summary>
public sealed class NebuLogUser : IdentityUser
{
}
