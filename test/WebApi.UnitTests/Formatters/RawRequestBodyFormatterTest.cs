using System.IO;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.Formatters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.UnitTests.Formatters;

/// <summary>
/// Pins the behavior of <see cref="RawRequestBodyFormatter"/> directly, without a hosted server.
/// <para>
/// The state endpoints carry <c>[Consumes("application/json", "text/json")]</c>, so this formatter's <see cref="RawRequestBodyFormatter.CanRead"/> never actually gets to decide anything for a request whose content type lets it through: <c>ConsumesAttribute</c> has already accepted or rejected it.
/// Its one live effect is what happens when a request carries no <c>Content-Type</c> header at all, since <c>ConsumesAttribute</c> does not reject that case.
/// A real <c>terraform apply</c> was captured on the wire for this change: every POST and DELETE it sends carries <c>Content-Type: application/json</c>, so that case is never the one Terraform hits.
/// </para>
/// <para>
/// These tests exist so that a change to this formatter, or to what <c>[Consumes]</c> lets through, shows up here first rather than as a state write that fails differently than expected.
/// </para>
/// </summary>
[Trait("Category", "UnitTests")]
public class RawRequestBodyFormatterTest
{
    [Theory]
    [InlineData("application/json")]
    [InlineData("text/json")]
    public void CanRead_WithAContentTypeConsumesLetsThrough_ReturnsFalse(string contentType)
    {
        // Arrange: these are the only two content types the controller accepts, and the only two a real terraform apply was observed to send
        var context = BuildContext(contentType);
        var formatter = new RawRequestBodyFormatter();

        // Act
        var canRead = formatter.CanRead(context);

        // Assert
        canRead.Should().BeFalse("the formatter must stay out of the way of every request terraform sends");
    }

    [Fact]
    public void CanRead_WithNoContentType_ReturnsTrue()
    {
        // Arrange: the one case ConsumesAttribute lets through unfiltered
        var context = BuildContext(null);
        var formatter = new RawRequestBodyFormatter();

        // Act & Assert
        formatter.CanRead(context).Should().BeTrue();
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/octet-stream")]
    public void CanRead_WithAContentTypeItOwns_ReturnsTrue(string contentType)
    {
        var context = BuildContext(contentType);
        var formatter = new RawRequestBodyFormatter();

        formatter.CanRead(context).Should().BeTrue();
    }

    [Fact]
    public void CanRead_WithAnUnrelatedContentType_ReturnsFalse()
    {
        var context = BuildContext("application/xml");
        var formatter = new RawRequestBodyFormatter();

        formatter.CanRead(context).Should().BeFalse();
    }

    [Fact]
    public async Task ReadRequestBodyAsync_WithNoContentType_ReadsTheBodyAsAPlainString()
    {
        // Arrange
        const string body = """{"version":4}""";
        var context = BuildContext(null, body);
        var formatter = new RawRequestBodyFormatter();

        // Act
        var result = await formatter.ReadRequestBodyAsync(context);

        // Assert: a raw string, not JSON, is what later fails to parse as a Terraform state
        result.HasError.Should().BeFalse();
        result.Model.Should().Be(body);
    }

    [Fact]
    public async Task ReadRequestBodyAsync_WithPlainTextContentType_ReadsTheBodyAsAPlainString()
    {
        var context = BuildContext("text/plain", "hello");
        var formatter = new RawRequestBodyFormatter();

        var result = await formatter.ReadRequestBodyAsync(context);

        result.HasError.Should().BeFalse();
        result.Model.Should().Be("hello");
    }

    [Fact]
    public async Task ReadRequestBodyAsync_WithOctetStreamContentType_ReadsTheBodyAsBytes()
    {
        var context = BuildContext("application/octet-stream", "hello");
        var formatter = new RawRequestBodyFormatter();

        var result = await formatter.ReadRequestBodyAsync(context);

        result.HasError.Should().BeFalse();
        result.Model.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("hello"));
    }

    private static InputFormatterContext BuildContext(string? contentType, string? body = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = contentType;

        if (body != null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            httpContext.Request.Body = new MemoryStream(bytes);
            httpContext.Request.ContentLength = bytes.Length;
        }

        return new InputFormatterContext(
            httpContext,
            modelName: "input",
            modelState: new ModelStateDictionary(),
            metadata: new EmptyModelMetadataProvider().GetMetadataForType(typeof(object)),
            readerFactory: (stream, encoding) => new StreamReader(stream, encoding));
    }
}
