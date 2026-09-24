using Microsoft.OpenApi;
using Withywoods.Configuration;

namespace Devpro.TerraformBackend.WebApi;

public class ApplicationConfiguration(IConfigurationRoot configurationRoot)
{
    public static string HealthCheckEndpoint => "/health";

    /// <summary>
    /// Bounds the database ping, which otherwise waits for the driver's 30 second server selection timeout
    /// while MongoDB is down.
    /// </summary>
    public static TimeSpan HealthCheckTimeout => TimeSpan.FromSeconds(5);

    public bool IsHttpsRedirectionEnabled => configurationRoot.TryGetSection<bool>("Features:IsHttpsRedirectionEnabled");

    public bool IsScalarEnabled => configurationRoot.TryGetSection<bool>("Features:IsScalarEnabled");

    public OpenApiInfo OpenApiInfo => configurationRoot.TryGetSection<OpenApiInfo>("OpenApi");

    public string ConnectionString => configurationRoot.TryGetSection<string>("DatabaseSettings:ConnectionString");

    public string DatabaseName => configurationRoot.TryGetSection<string>("DatabaseSettings:DatabaseName");

    /// <summary>
    /// How long a verified credential stays usable without running BCrypt again.
    /// Zero or less disables the cache.
    /// </summary>
    public TimeSpan CredentialCacheDuration =>
        TimeSpan.FromSeconds(configurationRoot.GetValue("Authentication:CredentialCacheSeconds", 60));

    /// <summary>
    /// Consecutive failures, counted per username and source address, before that pair is refused outright.
    /// </summary>
    public int MaxFailedAttempts => configurationRoot.GetValue("Authentication:MaxFailedAttempts", 10);

    public TimeSpan LockoutDuration =>
        TimeSpan.FromSeconds(configurationRoot.GetValue("Authentication:LockoutSeconds", 300));

    /// <summary>
    /// Addresses of the reverse proxies whose <c>X-Forwarded-For</c> the application may believe.
    /// </summary>
    public string[] KnownProxies =>
        configurationRoot.GetSection("Network:KnownProxies").Get<string[]>() ?? [];

    /// <summary>
    /// Networks, in CIDR notation, whose forwarded headers the application may believe.
    /// </summary>
    public string[] KnownNetworks =>
        configurationRoot.GetSection("Network:KnownNetworks").Get<string[]>() ?? [];

    /// <summary>
    /// Believes the forwarded headers of any caller.
    /// Needed in a cluster where the ingress address is not known in advance, and safe only where the
    /// application cannot be reached except through that ingress.
    /// </summary>
    public bool TrustAllProxies => configurationRoot.GetValue("Network:TrustAllProxies", false);
}
