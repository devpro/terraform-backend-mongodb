using Devpro.Common.AspNetCore.WebApi.Configuration;
using Microsoft.AspNetCore.Builder;

namespace Devpro.Common.AspNetCore.WebApi.Builder;

public static class SwaggerBuilderExtensions
{
    public static IApplicationBuilder UseSwagger(this IApplicationBuilder app, WebApiConfiguration configuration)
    {
        if (configuration.IsSwaggerEnabled)
        {
            var openApi = configuration.OpenApi;

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint($"/swagger/{openApi.Version}/swagger.json", $"{openApi.Title} {openApi.Version}"));
        }

        return app;
    }
}
