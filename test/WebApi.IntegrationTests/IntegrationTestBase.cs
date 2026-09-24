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
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.AspNetCore.Hosting;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests;

/// <summary>
/// Ref. https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
/// </summary>
public abstract class IntegrationTestBase(TestWebApplicationFactory factory)
    : DatabaseTestBase(factory)
{
    protected Faker Faker { get; } = new();

    /// <summary>
    /// A minimal Terraform state, with a fresh lineage so that no two tests share one.
    /// </summary>
    protected static object NewState() => new
    {
        version = 4,
        terraform_version = "1.9.0",
        serial = 1,
        lineage = Guid.NewGuid().ToString(),
        outputs = new { },
        resources = Array.Empty<object>()
    };

    protected Faker<StateLockModel> StateLockFaker { get; } = new Faker<StateLockModel>("en")
        .RuleFor(u => u.Id, _ => Guid.NewGuid().ToString())
        .RuleFor(o => o.Created, _ => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff+00:00"));

    /// <summary>
    /// Builds a client against the suite's host.
    /// <para>
    /// The Scalar feature flag is applied by <see cref="TestHostConfiguration"/> through <c>UseSetting</c>,
    /// which is scoped to one factory, never through an environment variable, which would leak across tests.
    /// </para>
    /// </summary>
    protected HttpClient CreateClient(bool isAuthorizationNeeded = false, Action<IWebHostBuilder>? builderConfiguration = null)
    {
        var client = (builderConfiguration == null) ? Factory.CreateClient()
            : Factory.WithWebHostBuilder(builderConfiguration).CreateClient();

        if (isAuthorizationNeeded)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes(
                    $"{TestCredentials.Username}:{TestCredentials.Password}")));
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

    /// <summary>
    /// A state name no other test or earlier run can be holding.
    /// <para>
    /// <c>tf_state</c> is uniquely indexed on <c>{tenant, name}</c>, and tests used to name states with
    /// <c>Faker.Random.Word()</c>, which repeats. A collision either fails the write or, worse, makes a test
    /// that expects a 404 find somebody else's state. The <c>test</c> prefix also guarantees the letter the
    /// route constraint requires, since a bare GUID may be all digits at the start.
    /// </para>
    /// </summary>
    protected static string UniqueStateName() => $"test{Guid.NewGuid():N}";

    protected static StringContent Serialize<T>(T value, string mediaType = "application/json")
    {
        return new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, mediaType);
    }
}
