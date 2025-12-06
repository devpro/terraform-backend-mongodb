using Devpro.Common.AspNetCore.WebApi.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Devpro.Common.AspNetCore.WebApi.DependencyInjection;

public static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddSwaggerGenWithBasicAuth(this IServiceCollection services, WebApiConfiguration configuration)
    {
        var openApi = configuration.OpenApi;

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(openApi.Version, new OpenApiInfo { Title = openApi.Title, Version = openApi.Version });

            options.AddSecurityDefinition("basic", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "basic",
                In = ParameterLocation.Header,
                Description = "Basic Authorization header using the Bearer scheme."
            });

            options.AddSecurityRequirement((document) => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("basic", document),
                    []
                }
            });
        });

        return services;
    }
}
