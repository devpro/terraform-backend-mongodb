using Microsoft.AspNetCore.Authorization;

namespace Devpro.Common.AspNetCore.WebApi.Authentication
{
    public class BasicAuthorizationAttribute : AuthorizeAttribute
    {
        public BasicAuthorizationAttribute()
        {
            AuthenticationSchemes = BasicAuthenticationDefaults.AuthenticationScheme;
        }
    }
}
