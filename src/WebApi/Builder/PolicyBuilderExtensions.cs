namespace Kalosyni.TerraformBackend.WebApi.Builder
{
    public static class PolicyBuilderExtensions
    {
        public static IApplicationBuilder UseHttpsRedirection(this IApplicationBuilder app, ApplicationConfiguration configuration)
        {
            if (configuration.IsHttpsRedirectionEnabled)
            {
                app.UseHttpsRedirection();
            }

            return app;
        }
    }
}
