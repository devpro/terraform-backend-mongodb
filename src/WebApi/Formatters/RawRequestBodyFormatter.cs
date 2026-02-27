using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Devpro.TerraformBackend.WebApi.Formatters;

/// <summary>
/// ASP.NET Core input formatter to manage raw request bodies.
/// </summary>
/// <remarks>
/// Solution found at https://weblog.west-wind.com/posts/2017/Sep/14/Accepting-Raw-Request-Body-Content-in-ASPNET-Core-API-Controllers
/// </remarks>
public class RawRequestBodyFormatter : InputFormatter
{
    private const string PlainTextContentType = "text/plain";

    private const string OctetStreamApplicationContentType = "application/octet-stream";

    public RawRequestBodyFormatter()
    {
        SupportedMediaTypes.Add(new MediaTypeHeaderValue(PlainTextContentType));
        SupportedMediaTypes.Add(new MediaTypeHeaderValue(OctetStreamApplicationContentType));
    }

    public override bool CanRead(InputFormatterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var contentType = context.HttpContext.Request.ContentType;
        return string.IsNullOrEmpty(contentType)
            || contentType == PlainTextContentType
            || contentType == OctetStreamApplicationContentType;
    }

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        var request = context.HttpContext.Request;

        if (string.IsNullOrEmpty(request.ContentType)
            || request.ContentType == PlainTextContentType)
        {
            using var reader = new StreamReader(request.Body);
            var plainContent = await reader.ReadToEndAsync();
            return await InputFormatterResult.SuccessAsync(plainContent);
        }

        if (request.ContentType != OctetStreamApplicationContentType)
        {
            return await InputFormatterResult.FailureAsync();
        }

        using var ms = new MemoryStream(2048);
        await request.Body.CopyToAsync(ms);
        var streamContent = ms.ToArray();
        return await InputFormatterResult.SuccessAsync(streamContent);
    }
}
