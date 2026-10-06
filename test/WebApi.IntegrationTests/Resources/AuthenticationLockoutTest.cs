using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.AspNetCore.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

/// <summary>
/// The failed-attempt lockout.
/// <para>
/// This lives in its own class deliberately, and in <see cref="NonParallelCollection"/> deliberately.
/// The lockout counter is stored in MongoDB, shared by every host in the run rather than reset per host,
/// so a test here that locks out <see cref="TestCredentials.Username"/> would leak into every other test authenticating as the same account if it ran concurrently with one.
/// <see cref="AuthenticationTimingTest"/> is the other test that sends wrong passwords for that account, which is why it shares this collection.
/// </para>
/// </summary>
[Trait("Category", "IntegrationTests")]
[Collection(NonParallelCollection.Name)]
public class AuthenticationLockoutTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int MaxFailedAttempts = 3;

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task Authentication_AfterTooManyFailures_RefusesEvenTheValidCredential()
    {
        // Arrange
        TrackLockout(TestCredentials.Username);
        var client = CreateThrottledClient();

        // Act: exhaust the allowance with wrong passwords
        for (var attempt = 0; attempt < MaxFailedAttempts; attempt++)
        {
            var failure = await Send(client, TestCredentials.Username, $"wrong-password-{attempt}");
            failure.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // Assert: the correct password is now refused, which is what proves the lockout is real rather than the credentials simply being wrong
        var response = await Send(client, TestCredentials.Username, TestCredentials.Password);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the account is locked out after {0} consecutive failures", MaxFailedAttempts);
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task Authentication_WhenAnotherAccountIsLockedOut_StillSucceeds()
    {
        // Arrange
        const string otherUsername = "someone-else";
        TrackLockout(otherUsername);
        var client = CreateThrottledClient();

        // Act: lock out a username that does not exist
        for (var attempt = 0; attempt < MaxFailedAttempts + 1; attempt++)
        {
            await Send(client, otherUsername, $"wrong-password-{attempt}");
        }

        // Assert: the lockout is scoped to the account it was triggered on, so a run by the real operator is unaffected.
        // A lockout scoped to the address alone would fail here, and one scoped to neither would let anybody on the internet deny service to the backend.
        var response = await Send(client, TestCredentials.Username, TestCredentials.Password);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task Authentication_WithUnknownUsername_IsRefusedWithoutError()
    {
        // Arrange: the dummy-hash verify that equalises the timing must not change the outcome
        const string unknownUsername = "no-such-user";
        TrackLockout(unknownUsername);
        var client = CreateThrottledClient();

        // Act
        var response = await Send(client, unknownUsername, "any-password");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A client on a host with a small allowance, so the test does not have to send the production default of ten failures per case.
    /// The threshold is per-host configuration, but the failure count it compares against is shared MongoDB state,
    /// so this only bounds how many requests this test sends, not how many failures another concurrent test contributes.
    /// </summary>
    private HttpClient CreateThrottledClient() => CreateClient(builderConfiguration: builder =>
    {
        builder.UseSetting("Authentication:MaxFailedAttempts", MaxFailedAttempts.ToString());
        builder.UseSetting("Authentication:LockoutSeconds", "300");
    });

    /// <summary>
    /// Registers the <c>auth_lockout</c> document this test creates for removal.
    /// The in-memory test server leaves the connection's remote address unset, so every request from this suite resolves to the same "unknown" address key;
    /// filtering on the username alone is what makes this robust to that without hardcoding the address representation.
    /// </summary>
    private void TrackLockout(string username) =>
        TrackDocumentsWhere("auth_lockout", Builders<BsonDocument>.Filter.Eq("username", username));

    private static Task<HttpResponseMessage> Send(HttpClient client, string username, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/{TestCredentials.Tenant}/state/{UniqueStateName()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
