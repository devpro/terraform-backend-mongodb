namespace Devpro.TerraformBackend.WebApi.Builder;

public static class SwaggerBuilderExtensions
{
    public static IApplicationBuilder UseSwagger(this IApplicationBuilder app, ApplicationConfiguration configuration)
    {
        if (!configuration.IsSwaggerEnabled)
        {
            return app;
        }

        var openApi = configuration.OpenApi;

        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint($"/swagger/{openApi.Version}/swagger.json", $"{openApi.Title} {openApi.Version}"));

        return app;
    }
}
