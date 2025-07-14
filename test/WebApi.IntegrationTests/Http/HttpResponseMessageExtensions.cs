using System.Net;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Http;

internal static class HttpResponseMessageExtensions
{
    public static async Task<string?> CheckResponseAndGetContent(this HttpResponseMessage response, HttpStatusCode expectedStatusCode, string? expectedContentType, string? expectedContent)
    {
        response.StatusCode.Should().Be(expectedStatusCode);

        if (expectedContentType == null)
        {
            response.Content.Headers.ContentType.Should().BeNull();
        }
        else
        {
            response.Content.Headers.ContentType.Should().NotBeNull();
            response.Content.Headers.ContentType?.ToString().Should().Be(expectedContentType);
        }

        var result = await response.Content.ReadAsStringAsync();

        if (expectedContent == null)
        {
            result.Should().BeNull();
        }
        else
        {
            result.Should().NotBeNull();
            result.Should().Be(expectedContent);
        }

        return result;
    }
}
