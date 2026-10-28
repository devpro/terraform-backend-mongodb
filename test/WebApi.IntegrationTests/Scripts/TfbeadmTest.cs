using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Scripts;

/// <summary>
/// The account creation path in <c>scripts/tfbeadm</c>, checked end to end: a user the script creates must be able to authenticate against the API.
/// <para>
/// The password used here contains a single quote, a dollar sign, a backslash and a space,
/// since each of them breaks a different hop of a value interpolated into a JavaScript string passed through two shells, which the script must never do.
/// The username case covers the same interpolation used as an injection.
/// </para>
/// </summary>
[Trait("Category", "IntegrationTests")]
public class TfbeadmTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    /// <summary>
    /// Every character that broke one of the hops between the shell and the stored document.
    /// </summary>
    private const string AwkwardPassword = @"pa'ss$w ord\""x";

    [Fact]
    public async Task Tfbeadm_CreatesAUserWhoseAwkwardPasswordAuthenticates()
    {
        // Arrange
        var username = $"script-user-{Guid.NewGuid():N}";
        TrackUser(username);

        // Act
        var result = await TfbeadmRunner.RunAsync(["create-user", username, TestCredentials.Tenant], AwkwardPassword,
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.Should().Be(0, "tfbeadm failed: {0}{1}", result.StandardOutput, result.StandardError);

        var response = await Authenticate(username, AwkwardPassword);
        response.Should().NotBe(HttpStatusCode.Unauthorized,
            "a user created by tfbeadm must be able to authenticate with the password that was given to it");
    }

    [Fact]
    public async Task Tfbeadm_DoesNotLeakThePasswordIntoItsOutput()
    {
        // Arrange
        var username = $"script-user-{Guid.NewGuid():N}";
        TrackUser(username);

        // Act
        var result = await TfbeadmRunner.RunAsync(["create-user", username, TestCredentials.Tenant], AwkwardPassword,
            TestContext.Current.CancellationToken);

        // Assert: the script echoes the MongoDB command it runs, which must not carry the credential material
        (result.StandardOutput + result.StandardError).Should().NotContain(AwkwardPassword);
    }

    [Fact]
    public async Task Tfbeadm_WithJavaScriptInTheUsername_DoesNotRunIt()
    {
        // Arrange: a username that closes the insertOne call and opens a second one, leaving the fields that follow it to complete the injected document.
        // The shape matters.
        // Appending a field proves nothing, since a duplicate key in a JavaScript object literal is won by the later one and the real values come after the username,
        // and terminating with a comment only produces a syntax error that aborts the whole command.
        // Against a script that interpolates the username, this payload creates a second account under a username and tenant of the caller's choosing.
        var marker = $"inject{Guid.NewGuid():N}";
        var username = $"x'}}); db.user.insertOne({{username: '{marker}', password_hash: '";
        TrackUser(username);
        TrackUser(marker);
        TrackUser("x");

        // Act: through the deprecated three-argument form, which the script still accepts
        await TfbeadmRunner.RunAsync(["create-user", username, AwkwardPassword, TestCredentials.Tenant], password: null,
            TestContext.Current.CancellationToken);

        // Assert: whether the script refuses the value or stores it verbatim, what it must never do is execute it.
        // An account an attacker can create is an account whose password they already know.
        var collection = Factory.Services.GetRequiredService<IMongoDatabase>().GetCollection<BsonDocument>("user");
        var injected = await collection.Find(Builders<BsonDocument>.Filter.Eq("username", marker))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        injected.Should().BeNull("a username must never be executed as JavaScript");
    }

    private void TrackUser(string username) =>
        TrackUserWhere(Builders<BsonDocument>.Filter.Eq("username", username));

    private void TrackUserWhere(FilterDefinition<BsonDocument> filter) => TrackDocumentsWhere("user", filter);

    private async Task<HttpStatusCode> Authenticate(string username, string password)
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/{TestCredentials.Tenant}/state/{UniqueStateName()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }
}
