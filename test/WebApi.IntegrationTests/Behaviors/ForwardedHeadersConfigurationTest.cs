using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Behaviors;

/// <summary>
/// The trusted proxy configuration, which the lockout and the authentication failure log both depend on.
/// <para>
/// This is asserted rather than assumed because the failure is silent and the consequence is not.
/// A key that does not bind leaves the application trusting loopback only, so behind an ingress every caller arrives as the proxy,
/// every one of them shares a single lockout bucket, and an attacker on the internet can lock the real operator out of the backend.
/// Nothing in the running application would say so.
/// </para>
/// <para>
/// The Helm chart renders these as <c>Network__KnownProxies__0</c> and <c>Network__KnownNetworks__0</c>, so the indexed form is what is exercised here.
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

        // the framework trusts loopback by default, and a configured allow list has to replace that rather than extend it, or the operator's list is not the list that applies
        options.KnownProxies.Should().HaveCount(1);
        options.KnownIPNetworks.Should().HaveCount(1);

        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public Task ForwardedHeaders_WhenTrustingAllProxies_ClearsEveryRestriction()
    {
        // Arrange & Act: an empty allow list is how ASP.NET Core is told to accept a forwarded header from any caller,
        // which is what a cluster needs when the ingress address is not known in advance
        var options = ResolveOptions(builder => builder.UseSetting("Network:TrustAllProxies", "true"));

        // Assert
        options.KnownProxies.Should().BeEmpty();
        options.KnownIPNetworks.Should().BeEmpty();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ForwardedHeaders_OnFailedAuthentication_CountTheFailureAgainstTheForwardedAddress()
    {
        // Arrange: a username nothing else uses, so the only counter it can have is the one this request creates
        const string forwardedAddress = "203.0.113.9";
        var username = $"forwarded-{Guid.NewGuid():N}";
        var filter = Builders<BsonDocument>.Filter.Eq("username", username);
        TrackDocumentsWhere("auth_lockout", filter);
        var client = CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/{TestCredentials.Tenant}/state/{UniqueStateName()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:wrong-password")));
        request.Headers.Add("X-Forwarded-For", forwardedAddress);

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert: correct options are not enough, since the credential is checked by a middleware of its own,
        // and one that runs ahead of the forwarded headers counts every caller against the proxy
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var counter = await Factory.Services.GetRequiredService<IMongoDatabase>()
            .GetCollection<BsonDocument>("auth_lockout")
            .Find(filter)
            .SingleAsync(TestContext.Current.CancellationToken);
        counter["remote_address"].AsString.Should().Be(forwardedAddress);
    }

    private ForwardedHeadersOptions ResolveOptions(System.Action<IWebHostBuilder> configuration)
    {
        using var configured = Factory.WithWebHostBuilder(configuration);
        return configured.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}
