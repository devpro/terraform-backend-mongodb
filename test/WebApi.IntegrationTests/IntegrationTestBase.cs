using System.Net.Http;
using Bogus;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests
{
    /// <summary>
    /// See https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
    /// </summary>
    public abstract class IntegrationTestBase(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
    {
        protected Faker Faker { get; } = new("en");

        protected HttpClient CreateClient()
        {
            return factory.CreateClient();
        }
    }
}
