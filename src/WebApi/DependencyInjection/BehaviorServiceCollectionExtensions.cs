using Microsoft.AspNetCore.Mvc;

namespace Devpro.TerraformBackend.WebApi.DependencyInjection;

public static class BehaviorServiceCollectionExtensions
{
    /// <summary>
    /// Logs a warning with the request path and the validation errors whenever a request fails model validation,
    /// since the framework answers <c>400</c> before any controller code runs and would otherwise leave no trace.
    /// </summary>
    public static void AddInvalidModelStateLog(this IServiceCollection services)
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
    }
}
