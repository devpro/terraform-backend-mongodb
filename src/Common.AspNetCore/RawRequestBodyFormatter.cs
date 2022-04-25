using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Kalosyni.Common.AspNetCore
{
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
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

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
                var content = await reader.ReadToEndAsync();
                return await InputFormatterResult.SuccessAsync(content);
            }

            if (request.ContentType == OctetStreamApplicationContentType)
            {
                using var ms = new MemoryStream(2048);
                await request.Body.CopyToAsync(ms);
                var content = ms.ToArray();
                return await InputFormatterResult.SuccessAsync(content);
            }

            return await InputFormatterResult.FailureAsync();
        }
    }
}
