using Devpro.Common.AspNetCore.WebApi.Authentication;
using Devpro.TerraformBackend.WebApi.Authentication;
using Microsoft.AspNetCore.Authentication;

namespace Devpro.TerraformBackend.WebApi.DependencyInjection;

public static class AuthenticationServiceCollectionExtensions
{
    public static AuthenticationBuilder AddBasicAuthentication(this IServiceCollection services)
    {
        return services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationDefaults.AuthenticationScheme, null);
    }
}
