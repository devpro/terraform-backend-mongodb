using System.Security.Claims;

namespace Devpro.TerraformBackend.WebApi.Authentication;

public static class ClaimsPrincipalExtensions
{
    public const string Tenant = "Tenant";

    public static bool IsInTenant(this ClaimsPrincipal claimsPrincipal, string tenant)
    {
        return claimsPrincipal.FindFirst(Tenant)?.Value == tenant;
    }
}
