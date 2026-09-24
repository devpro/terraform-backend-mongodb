using Microsoft.OpenApi;

namespace Devpro.TerraformBackend.WebApi.DependencyInjection;

public static class OpenApiServiceCollectionExtensions
{
    /// <summary>
    /// Major and minor of <c>VersionPrefix</c>, so the document follows the release without a second number to
    /// keep in step, and without the per-build suffix a snapshot could not match.
    /// </summary>
    private static string DocumentVersion
    {
        get
        {
            var version = typeof(OpenApiServiceCollectionExtensions).Assembly.GetName().Version!;
            return $"v{version.Major}.{version.Minor}";
        }
    }

    public static void AddOpenApiWithBasicAuth(this IServiceCollection services, ApplicationConfiguration configuration)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((doc, _, _) =>
            {
                doc.Info = configuration.OpenApiInfo;
                doc.Info.Version = DocumentVersion;
                doc.Components = new OpenApiComponents();
                doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                doc.Components.SecuritySchemes["basic"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "basic",
                    In = ParameterLocation.Header,
                    Name = "Authorization",
                    Description = "Basic Auth: username:password (base64 encoded)"
                };
                doc.Security ??= new List<OpenApiSecurityRequirement>();
                doc.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("basic")] = []
                });

                return Task.CompletedTask;
            });
        });
    }
}
