using Microsoft.AspNetCore.Mvc;

namespace Devpro.TerraformBackend.WebApi.DependencyInjection
{
    internal static class BehaviorExtensions
    {
        internal static IServiceCollection AddBehaviors(this IServiceCollection services)
        {
            services.PostConfigure<ApiBehaviorOptions>(options =>
            {
                var builtInFactory = options.InvalidModelStateResponseFactory;

                options.InvalidModelStateResponseFactory = context =>
                {
                    var loggerFactory = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger("PostConfigure");

                    logger.LogWarning("Invalid model {RequestPath}", context.HttpContext.Request.Path);

                    return builtInFactory(context);
                };
            });

            return services;
        }
    }
}
