namespace Devpro.TerraformBackend.WebApi.Builder
{
    public static class SwaggerBuilderExtensions
    {
        public static IApplicationBuilder UseSwagger(this IApplicationBuilder app, ApplicationConfiguration configuration)
        {
            if (configuration.IsSwaggerEnabled)
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            return app;
        }
    }
}
