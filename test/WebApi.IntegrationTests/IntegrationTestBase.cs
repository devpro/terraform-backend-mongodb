using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Bogus;
using Devpro.TerraformBackend.Domain.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests;

/// <summary>
/// Ref. https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
/// </summary>
public abstract class IntegrationTestBase(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    protected Faker Faker { get; } = new();

    protected Faker<StateModel> StateFaker { get; } = new("en");

    protected Faker<StateLockModel> StateLockFaker { get; } = new Faker<StateLockModel>("en")
        .RuleFor(u => u.Id, _ => Guid.NewGuid().ToString())
        .RuleFor(o => o.Created, _ => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff+00:00"));

    protected HttpClient CreateClient(bool isAuthorizationNeeded = false, Action<IWebHostBuilder>? builderConfiguration = null)
    {
        // ref. https://blog.markvincze.com/overriding-configuration-in-asp-net-core-integration-tests/
        Environment.SetEnvironmentVariable("Application__IsScalarEnabled", "true");

        var client = (builderConfiguration == null) ? factory.CreateClient()
            : factory.WithWebHostBuilder(builderConfiguration).CreateClient();

        if (isAuthorizationNeeded)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "admin", "admin123"))));
        }

        return client;
    }

    protected static async Task<string?> CheckResponseAndGetContentAsync(HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        string? expectedContentType,
        string? expectedContent = null,
        bool isRegexMatch = false,
        CancellationToken cancellationToken = default)
    {
        var result = await response.Content.ReadAsStringAsync(cancellationToken);

        if (expectedContent != null)
        {
            result.Should().NotBeNull();
            if (isRegexMatch)
            {
                result.Should().MatchRegex(expectedContent);
            }
            else
            {
                result.Should().Be(expectedContent);
            }
        }

        if (expectedContentType == null)
        {
            response.Content.Headers.ContentType.Should().BeNull();
        }
        else
        {
            response.Content.Headers.ContentType.Should().NotBeNull();
            response.Content.Headers.ContentType?.ToString().Should().Be(expectedContentType);
        }

        response.StatusCode.Should().Be(expectedStatusCode);

        return result;
    }

    protected static StringContent Serialize<T>(T value, string mediaType = "application/json")
    {
        return new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, mediaType);
    }
}
