using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Devpro.TerraformBackend.WebApi.Filters;

/// <summary>
/// Refuses a request whose route tenant is not the caller's.
/// <para>
/// An authorization filter rather than an action filter, because an action filter runs after model binding,
/// so a caller from another tenant would have its body, up to the request size limit, read and parsed before being refused.
/// </para>
/// </summary>
public class TenantAuthorizationFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var tenant = context.RouteData.Values["tenant"]?.ToString();

        if (string.IsNullOrEmpty(tenant) || !context.HttpContext.User.IsInTenant(tenant))
        {
            context.Result = new UnauthorizedResult();
        }
    }
}
