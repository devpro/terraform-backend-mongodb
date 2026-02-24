using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi;

namespace Devpro.Common.AspNetCore.WebApi.Configuration;

public class WebApiConfiguration(IConfigurationRoot configurationRoot)
{
    protected IConfigurationRoot ConfigurationRoot { get; } = configurationRoot;

    // flags

    public bool IsHttpsRedirectionEnabled => TryGetSection<bool>("Application:IsHttpsRedirectionEnabled");

    public bool IsSwaggerEnabled => TryGetSection<bool>("Application:IsSwaggerEnabled");

    // definitions

    public static string HealthCheckEndpoint => "/health";

    public OpenApiInfo OpenApi => TryGetSection<OpenApiInfo>("OpenApi");

    // protected methods

    protected T TryGetSection<T>(string sectionKey)
    {
        var section = ConfigurationRoot.GetSection(sectionKey)
            ?? throw new ArgumentException($"Missing section \"{sectionKey}\" in configuration", nameof(sectionKey));
        return section.Get<T>()
            ?? throw new ArgumentException($"Section \"{sectionKey}\" value cannot be read as \"{nameof(T)}\"", nameof(sectionKey));
    }
}
