using Microsoft.OpenApi;

namespace Devpro.TerraformBackend.WebApi;

public class ApplicationConfiguration(IConfigurationRoot configurationRoot)
{
    // properties

    public static string HealthCheckEndpoint => "/health";

    public bool IsHttpsRedirectionEnabled => TryGetSection<bool>("Features:IsHttpsRedirectionEnabled");

    public bool IsSwaggerEnabled => TryGetSection<bool>("Features:IsSwaggerEnabled");

    public OpenApiInfo OpenApi => TryGetSection<OpenApiInfo>("OpenApi");

    public string ConnectionString => TryGetSection<string>("DatabaseSettings:ConnectionString");

    public string DatabaseName => TryGetSection<string>("DatabaseSettings:DatabaseName");

    // protected methods

    private T TryGetSection<T>(string sectionKey)
    {
        var section = configurationRoot.GetSection(sectionKey)
                      ?? throw new InvalidOperationException($"Missing section \"{sectionKey}\" in configuration");
        return section.Get<T>()
               ?? throw new InvalidOperationException($"Section \"{sectionKey}\" value cannot be read as \"{nameof(T)}\"");
    }
}
