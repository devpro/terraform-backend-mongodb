using Farseer.Common.AspNetCore.WebApi.Authentication;
using Farseer.TerraformBackend.WebApi.Authentication;
using Microsoft.AspNetCore.Authentication;

namespace Farseer.TerraformBackend.WebApi.DependencyInjection;

public static class AuthenticationServiceCollectionExtensions
{
    public static AuthenticationBuilder AddBasicAuthentication(this IServiceCollection services)
    {
        return services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationDefaults.AuthenticationScheme, null);
    }
}
