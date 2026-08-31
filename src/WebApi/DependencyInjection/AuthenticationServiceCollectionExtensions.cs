using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devpro.TerraformBackend.WebApi.DependencyInjection;

public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the credential cache that sits in front of the BCrypt verify, and validates the failed-attempt
    /// lockout thresholds that <see cref="Authentication.ThrottledCredentialAuthenticator"/> enforces against
    /// <c>auth_lockout</c> in MongoDB.
    /// <para>
    /// Validated here, before registering anything, so a value that would defeat the lockout fails at startup
    /// rather than as a silent runtime surprise.
    /// </para>
    /// </summary>
    public static void AddCredentialAuthentication(this IServiceCollection services, ApplicationConfiguration configuration)
    {
        if (configuration.MaxFailedAttempts < 1)
        {
            // a threshold below 1 locks out a pair on its very first attempt, indistinguishable from every
            // account being permanently refused
            throw new InvalidOperationException(
                $"Authentication:MaxFailedAttempts must be at least 1, but is {configuration.MaxFailedAttempts}.");
        }

        if (configuration.LockoutDuration <= TimeSpan.Zero)
        {
            // a lockout that expires immediately never actually withholds access, which defeats S1 silently
            throw new InvalidOperationException(
                $"Authentication:LockoutSeconds must be greater than zero, but is {configuration.LockoutDuration.TotalSeconds}.");
        }

        // the lockout itself lives in MongoDB, not here: this cache now only ever holds a successful
        // credential's verification result, and an attacker cannot grow it, since only a verification that
        // already succeeded is ever stored. The bound is a defensive cap on legitimate traffic, not a defence
        // against a flood of distinct usernames, which auth_lockout's TTL index already handles.
        services.AddMemoryCache(options => options.SizeLimit = 10_000);

        // scoped, not singleton: the repository it verifies through is scoped, and the one piece of state that
        // has to outlive a request within a single pod, the credential cache, already lives in the singleton
        // IMemoryCache rather than in this service
        services.TryAddScoped<ICredentialAuthenticator, ThrottledCredentialAuthenticator>();
    }

    /// <summary>
    /// Configures which reverse proxies the application believes when it reads the caller's address.
    /// <para>
    /// This is a prerequisite for the lockout and for the authentication failure log, not a detail. Behind an
    /// ingress, an unconfigured application sees the proxy's address on every request, so every caller shares
    /// one lockout bucket and every logged address names the proxy instead of the attacker.
    /// </para>
    /// <para>
    /// ASP.NET Core trusts only loopback by default, which is why the configured values have to replace that
    /// default rather than extend it.
    /// </para>
    /// </summary>
    public static void AddTrustedProxies(this IServiceCollection services, ApplicationConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            if (configuration.TrustAllProxies)
            {
                // an empty allow list makes ASP.NET Core accept forwarded headers from any caller, which is
                // only safe where the application is reachable through the ingress alone
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                return;
            }

            if (configuration.KnownProxies.Length == 0 && configuration.KnownNetworks.Length == 0)
            {
                return;
            }

            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var proxy in configuration.KnownProxies)
            {
                if (IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }

            foreach (var network in configuration.KnownNetworks)
            {
                if (System.Net.IPNetwork.TryParse(network, out var parsed))
                {
                    options.KnownIPNetworks.Add(parsed);
                }
            }
        });
    }
}
