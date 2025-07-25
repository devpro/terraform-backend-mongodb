using Devpro.Common.AspNetCore.WebApi.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Devpro.Common.AspNetCore.WebApi.DependencyInjection;

public static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddSwaggerGenWithBasicAuth(this IServiceCollection services, WebApiConfiguration configuration)
    {
        var openApi = configuration.OpenApi;

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc(openApi.Version, new OpenApiInfo { Title = openApi.Title, Version = openApi.Version });
            c.AddSecurityDefinition("basic", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "basic",
                In = ParameterLocation.Header,
                Description = "Basic Authorization header using the Bearer scheme."
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "basic"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
