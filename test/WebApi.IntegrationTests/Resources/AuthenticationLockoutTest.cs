using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

/// <summary>
/// The failed-attempt lockout.
/// <para>
/// This lives in its own class deliberately. The lockout state is held in the host's memory cache, and a
/// class fixture gives one host per test class, so locking an account out here cannot leak into the tests
/// that authenticate as the same account elsewhere.
/// </para>
/// </summary>
[Trait("Category", "IntegrationTests")]
public class AuthenticationLockoutTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int MaxFailedAttempts = 3;

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task Authentication_AfterTooManyFailures_RefusesEvenTheValidCredential()
    {
        // Arrange
        var client = CreateThrottledClient();

        // Act: exhaust the allowance with wrong passwords
        for (var attempt = 0; attempt < MaxFailedAttempts; attempt++)
        {
            var failure = await Send(client, TestCredentials.Username, $"wrong-password-{attempt}");
            failure.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // Assert: the correct password is now refused, which is what proves the lockout is real rather than
        // the credentials simply being wrong
        var response = await Send(client, TestCredentials.Username, TestCredentials.Password);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the account is locked out after {0} consecutive failures", MaxFailedAttempts);
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task Authentication_WhenAnotherAccountIsLockedOut_StillSucceeds()
    {
        // Arrange
        var client = CreateThrottledClient();

        // Act: lock out a username that does not exist
        for (var attempt = 0; attempt < MaxFailedAttempts + 1; attempt++)
        {
            await Send(client, "someone-else", $"wrong-password-{attempt}");
        }

        // Assert: the lockout is scoped to the account it was triggered on, so a run by the real operator is
        // unaffected. A lockout scoped to the address alone would fail here, and one scoped to neither would
        // let anybody on the internet deny service to the backend.
        var response = await Send(client, TestCredentials.Username, TestCredentials.Password);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task Authentication_WithUnknownUsername_IsRefusedWithoutError()
    {
        // Arrange: the dummy-hash verify that equalises the timing must not change the outcome
        var client = CreateThrottledClient();

        // Act
        var response = await Send(client, "no-such-user", "any-password");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A client on a host with a small allowance, so the test does not have to send the production default of
    /// ten failures per case. Built once per test, since each call builds its own host and therefore its own
    /// lockout state.
    /// </summary>
    private HttpClient CreateThrottledClient() => CreateClient(builderConfiguration: builder =>
    {
        builder.UseSetting("Authentication:MaxFailedAttempts", MaxFailedAttempts.ToString());
        builder.UseSetting("Authentication:LockoutSeconds", "300");
    });

    private static Task<HttpResponseMessage> Send(HttpClient client, string username, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/{TestCredentials.Tenant}/state/{UniqueStateName()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
