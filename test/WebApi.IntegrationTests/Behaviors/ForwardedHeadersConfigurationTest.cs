using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Behaviors;

/// <summary>
/// The trusted proxy configuration, which the lockout and the authentication failure log both depend on.
/// <para>
/// This is asserted rather than assumed because the failure is silent and the consequence is not. A key that
/// does not bind leaves the application trusting loopback only, so behind an ingress every caller arrives as
/// the proxy, every one of them shares a single lockout bucket, and an attacker on the internet can lock the
/// real operator out of the backend. Nothing in the running application would say so.
/// </para>
/// <para>
/// The Helm chart renders these as <c>Network__KnownProxies__0</c> and <c>Network__KnownNetworks__0</c>, so
/// the indexed form is what is exercised here.
/// </para>
/// </summary>
[Trait("Category", "IntegrationTests")]
public class ForwardedHeadersConfigurationTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    [Trait("Mode", "Readonly")]
    public Task ForwardedHeaders_WithConfiguredProxies_TrustsExactlyThose()
    {
        // Arrange & Act
        var options = ResolveOptions(builder =>
        {
            builder.UseSetting("Network:KnownProxies:0", "10.0.0.7");
            builder.UseSetting("Network:KnownNetworks:0", "10.42.0.0/16");
        });

        // Assert
        options.KnownProxies.Select(x => x.ToString()).Should().Contain("10.0.0.7");
        options.KnownIPNetworks.Select(x => x.ToString()).Should().Contain("10.42.0.0/16");

        // the framework trusts loopback by default, and a configured allow list has to replace that rather
        // than extend it, or the operator's list is not the list that applies
        options.KnownProxies.Should().HaveCount(1);
        options.KnownIPNetworks.Should().HaveCount(1);

        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public Task ForwardedHeaders_WhenTrustingAllProxies_ClearsEveryRestriction()
    {
        // Arrange & Act: an empty allow list is how ASP.NET Core is told to accept a forwarded header from
        // any caller, which is what a cluster needs when the ingress address is not known in advance
        var options = ResolveOptions(builder => builder.UseSetting("Network:TrustAllProxies", "true"));

        // Assert
        options.KnownProxies.Should().BeEmpty();
        options.KnownIPNetworks.Should().BeEmpty();

        return Task.CompletedTask;
    }

    private ForwardedHeadersOptions ResolveOptions(System.Action<IWebHostBuilder> configuration)
    {
        using var configured = Factory.WithWebHostBuilder(configuration);
        return configured.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}
