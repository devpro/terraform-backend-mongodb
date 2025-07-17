using System.Security.Claims;

namespace Farseer.TerraformBackend.WebApi.Authentication;

public static class ClaimsPrincipalExtensions
{
    public const string Tenant = "Tenant";

    public static bool IsInTenant(this ClaimsPrincipal claimsPrincipal, string tenant)
    {
        return claimsPrincipal.FindFirst(Tenant)?.Value == tenant;
    }
}
