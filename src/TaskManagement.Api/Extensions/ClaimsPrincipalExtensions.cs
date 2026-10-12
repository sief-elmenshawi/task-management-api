using System.Security.Claims;

namespace TaskManagement.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out var userId) ? userId : null;
}
