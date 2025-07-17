using Microsoft.AspNetCore.Authorization;

namespace Farseer.Common.AspNetCore.WebApi.Authentication;

public class BasicAuthorizationAttribute : AuthorizeAttribute
{
    public BasicAuthorizationAttribute()
    {
        AuthenticationSchemes = BasicAuthenticationDefaults.AuthenticationScheme;
    }
}
