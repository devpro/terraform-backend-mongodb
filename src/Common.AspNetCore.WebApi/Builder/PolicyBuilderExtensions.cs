using Farseer.Common.AspNetCore.WebApi.Configuration;
using Microsoft.AspNetCore.Builder;

namespace Farseer.Common.AspNetCore.WebApi.Builder;

public static class PolicyBuilderExtensions
{
    public static IApplicationBuilder UseHttpsRedirection(this IApplicationBuilder app, WebApiConfiguration configuration)
    {
        if (configuration.IsHttpsRedirectionEnabled)
        {
            app.UseHttpsRedirection();
        }

        return app;
    }
}
