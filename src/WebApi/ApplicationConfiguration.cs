using Microsoft.OpenApi;
using Withywoods.Configuration;

namespace Devpro.TerraformBackend.WebApi;

public class ApplicationConfiguration(IConfigurationRoot configurationRoot)
{
    public static string HealthCheckEndpoint => "/health";

    public bool IsHttpsRedirectionEnabled => configurationRoot.TryGetSection<bool>("Features:IsHttpsRedirectionEnabled");

    public bool IsScalarEnabled => configurationRoot.TryGetSection<bool>("Features:IsScalarEnabled");

    public OpenApiInfo OpenApiInfo => configurationRoot.TryGetSection<OpenApiInfo>("OpenApi");

    public string ConnectionString => configurationRoot.TryGetSection<string>("DatabaseSettings:ConnectionString");

    public string DatabaseName => configurationRoot.TryGetSection<string>("DatabaseSettings:DatabaseName");
}
