using System.Net.Http.Headers;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests
{
    /// <summary>
    /// See https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
    /// </summary>
    public abstract class IntegrationTestBase(WebApplicationFactory<Program> factory)
        : IClassFixture<WebApplicationFactory<Program>>
    {
        protected Faker Faker { get; } = new("en");

        protected HttpClient CreateClient(bool isAuthorizationNeeded = false)
        {
            var client = factory.CreateClient();

            if (isAuthorizationNeeded)
            {
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "admin", "admin")));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            }

            return client;
        }

        protected StringContent GeneratePayload()
        {
            var dummy = new
            {
                Property1 = Faker.Random.String(),
                Property2 = Faker.Random.Int()
            };
            return new StringContent(JsonSerializer.Serialize(dummy), Encoding.UTF8, "application/json");
        }
    }
}
