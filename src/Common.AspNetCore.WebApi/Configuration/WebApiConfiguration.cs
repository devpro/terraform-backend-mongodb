using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;

namespace Devpro.Common.AspNetCore.WebApi.Configuration
{
    public class WebApiConfiguration
    {
        protected IConfigurationRoot ConfigurationRoot { get; }

        public WebApiConfiguration(IConfigurationRoot configurationRoot)
        {
            ConfigurationRoot = configurationRoot;
        }

        // flags

        public bool IsOpenTelemetryEnabled => TryGetSection("Application:IsOpenTelemetryEnabled").Get<bool>();

        public bool IsHttpsRedirectionEnabled => TryGetSection("Application:IsHttpsRedirectionEnabled").Get<bool>();

        public bool IsSwaggerEnabled => TryGetSection("Application:IsSwaggerEnabled").Get<bool>();


        // definitions

        public static string HealthCheckEndpoint => "/health";

        public OpenApiInfo OpenApi => TryGetSection("OpenApi").Get<OpenApiInfo>() ?? throw new Exception("");

        public string OpenTelemetryService => TryGetSection("OpenTelemetry:ServiceName").Get<string>() ?? "";

        // infrastructure

        public string OpenTelemetryCollectorEndpoint => TryGetSection("OpenTelemetry:CollectorEndpoint").Get<string>() ?? "";

        // protected methods

        protected IConfigurationSection TryGetSection(string sectionKey)
        {
            return ConfigurationRoot.GetSection(sectionKey)
                ?? throw new ArgumentException("Missing section \"" + sectionKey + "\" in configuration", nameof(sectionKey));
        }
    }
}
