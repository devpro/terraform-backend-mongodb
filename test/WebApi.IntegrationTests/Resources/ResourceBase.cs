using System;
using System.Net.Http;
using AutoFixture;
using Microsoft.AspNetCore.Mvc.Testing;
using Withywoods.WebTesting.Rest;
using Xunit;
using Xunit.Abstractions;

namespace Kalosyni.TerraformBackend.WebApi.IntegrationTests.Resources
{
    public class ResourceBase : RestClient, IClassFixture<WebApplicationFactory<Program>>
    {
        protected ResourceBase(WebApplicationFactory<Program> factory, ITestOutputHelper testOutput)
            : base(TestConfiguration.IsLocalhostEnvironment ? factory.CreateClient() : new HttpClient { BaseAddress = new Uri(TestConfiguration.ApiUrl) })
        {
            TestOutput = testOutput;
        }

        protected TestConfiguration Configuration { get; } = new();

        protected Fixture Fixture { get; } = new();

        protected ITestOutputHelper TestOutput { get; }
    }
}
