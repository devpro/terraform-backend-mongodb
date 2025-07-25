using Devpro.TerraformBackend.WebApi.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Devpro.TerraformBackend.WebApi.Filters;

public class TenantAuthorizationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var tenant = context.RouteData.Values["tenant"]?.ToString();

        if (string.IsNullOrEmpty(tenant) || !context.HttpContext.User.IsInTenant(tenant))
        {
            context.Result = new UnauthorizedResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // No action needed after execution
    }
}
