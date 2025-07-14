using System.Net;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Http;

internal static class HttpResponseMessageExtensions
{
    public static async Task<string?> CheckResponseAndGetContent(this HttpResponseMessage response,
        HttpStatusCode expectedStatusCode, string? expectedContentType, string? expectedContent = null)
    {
        var result = await response.Content.ReadAsStringAsync();

        if (expectedContent != null)
        {
            result.Should().NotBeNull();
            result.Should().Be(expectedContent);
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
}
