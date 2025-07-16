using System.Net.Http.Headers;
using Devpro.TerraformBackend.Domain.Models;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests;

/// <summary>
/// Ref. https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
/// </summary>
public abstract class IntegrationTestBase(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    protected Faker Faker { get; } = new("en");

    protected Faker<StateModel> StateFaker { get; } = new Faker<StateModel>("en");

    protected Faker<StateLockModel> StateLockFaker { get; } = new Faker<StateLockModel>("en")
        .RuleFor(u => u.Id, f => Guid.NewGuid().ToString())
        .RuleFor(o => o.Created, f => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff+00:00"));

    protected HttpClient CreateClient(bool isAuthorizationNeeded = false)
    {
        // ref. https://blog.markvincze.com/overriding-configuration-in-asp-net-core-integration-tests/
        Environment.SetEnvironmentVariable("Application__IsSwaggerEnabled", "true");

        var client = factory.CreateClient();

        if (isAuthorizationNeeded)
        {
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "admin", "admin")));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
        }

        return client;
    }

    protected static StringContent Serialize<T>(T value, string mediaType = "application/json")
    {
        return new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, mediaType);
    }
}
