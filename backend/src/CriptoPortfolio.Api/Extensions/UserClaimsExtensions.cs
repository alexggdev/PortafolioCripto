using System.Security.Claims;

namespace CriptoPortfolio.Api.Extensions;

public static class UserClaimsExtensions
{
    /// <summary>Id del usuario autenticado (claim "sub"), o null si no hay o no es un Guid.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
