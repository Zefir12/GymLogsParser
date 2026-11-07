using System.Security.Claims;

namespace GymLogsParser.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The owner of the signed-in user's data: their OIDC <c>sub</c> (#45). Null when the
    /// request is not authenticated or the token had no <c>sub</c>.
    /// </summary>
    public static string? OwnerId(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.FindFirst("sub")?.Value is { Length: > 0 } sub
            ? sub
            : null;
}
