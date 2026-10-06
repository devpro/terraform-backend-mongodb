using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devpro.TerraformBackend.WebApi.DependencyInjection;

public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the credential cache that sits in front of the BCrypt verify,
    /// and validates the failed-attempt lockout thresholds that <see cref="Authentication.ThrottledCredentialAuthenticator"/> enforces against <c>auth_lockout</c> in MongoDB.
    /// <para>
    /// Validated before registering anything, so a value that would defeat the lockout fails at startup.
    /// </para>
    /// </summary>
    public static void AddCredentialAuthentication(this IServiceCollection services, ApplicationConfiguration configuration)
    {
        if (configuration.MaxFailedAttempts < 1)
        {
            // below 1, every pair is locked out on its first attempt
            throw new InvalidOperationException(
                $"Authentication:MaxFailedAttempts must be at least 1, but is {configuration.MaxFailedAttempts}.");
        }

        if (configuration.LockoutDuration <= TimeSpan.Zero)
        {
            // a lockout that expires immediately never withholds access
            throw new InvalidOperationException(
                $"Authentication:LockoutSeconds must be greater than zero, but is {configuration.LockoutDuration.TotalSeconds}.");
        }

        // only a successful verification is ever cached, so an attacker cannot grow the cache, and the bound is a cap on legitimate traffic
        services.AddMemoryCache(options => options.SizeLimit = 10_000);

        // scoped, since the repositories it calls are scoped and its only lasting state is the singleton cache
        services.TryAddScoped<ICredentialAuthenticator, ThrottledCredentialAuthenticator>();
    }

    /// <summary>
    /// Configures which reverse proxies the application believes when it reads the caller's address.
    /// <para>
    /// Behind an unconfigured ingress, every request carries the proxy's address, so every caller shares one lockout bucket and every failure is logged against the proxy.
    /// The configured values replace the loopback-only default rather than extend it,
    /// which is why an entry that does not parse fails at startup: skipping it would leave nothing trusted.
    /// </para>
    /// </summary>
    public static void AddTrustedProxies(this IServiceCollection services, ApplicationConfiguration configuration)
    {
        var proxies = configuration.KnownProxies.Select(value => IPAddress.TryParse(value, out var address)
            ? address
            : throw new InvalidOperationException($"Network:KnownProxies holds '{value}', which is not an IP address."))
            .ToList();
        var networks = configuration.KnownNetworks.Select(value => System.Net.IPNetwork.TryParse(value, out var network)
            ? network
            : throw new InvalidOperationException($"Network:KnownNetworks holds '{value}', which is not a network in CIDR notation."))
            .ToList();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            if (configuration.TrustAllProxies)
            {
                // an empty allow list makes ASP.NET Core accept forwarded headers from any caller, which is only safe where the application is reachable through the ingress alone
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                return;
            }

            if (proxies.Count == 0 && networks.Count == 0)
            {
                return;
            }

            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            proxies.ForEach(options.KnownProxies.Add);
            networks.ForEach(options.KnownIPNetworks.Add);
        });
    }
}
