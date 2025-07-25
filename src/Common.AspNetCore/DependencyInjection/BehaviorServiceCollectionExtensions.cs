using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Devpro.Common.AspNetCore.DependencyInjection;

public static class BehaviorServiceCollectionExtensions
{
    /// <summary>
    /// Ensures that every time an invalid model state occurs in the API, a warning log is generated with the request path.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddInvalidModelStateLog(this IServiceCollection services)
    {
        services.PostConfigure<ApiBehaviorOptions>(options =>
        {
            var defaultFactory = options.InvalidModelStateResponseFactory;

            options.InvalidModelStateResponseFactory = context =>
            {
                var loggerFactory = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger(nameof(BehaviorServiceCollectionExtensions));

                var errors = context.ModelState
                    .Where(m => m.Value?.Errors.Any() == true)
                    .Select(m => new { Field = m.Key, Errors = m.Value!.Errors.Select(e => e.ErrorMessage) });

                logger.LogWarning("Invalid model state for {RequestPath}. Validation errors {@ModelErrors}", context.HttpContext.Request.Path, errors);

                return defaultFactory(context);
            };
        });

        return services;
    }
}
