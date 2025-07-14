using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Devpro.Common.AspNetCore.WebApi.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Devpro.TerraformBackend.WebApi.Authentication;

public class BasicAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // checks authorization header
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));
        }

        var authorizationHeader = Request.Headers.Authorization.ToString();

        // checks authorization header starts with Basic
        if (!authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Authorization header does not start with 'Basic'"));
        }

        // decrypts the authorization header and split out the client id/secret
        var authBase64Decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorizationHeader.Replace("Basic ", "", StringComparison.OrdinalIgnoreCase)));
        var authSplit = authBase64Decoded.Split([':'], 2);
        if (authSplit.Length != 2)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header format"));
        }

        var clientId = authSplit[0];
        var clientSecret = authSplit[1];

        // TODO: store this info in the database & restrict a user to its organization
        // checkClient ID and secret are incorrect
        if (clientId != "admin" || clientSecret != "admin")
        {
            return Task.FromResult(AuthenticateResult.Fail(string.Format("The secret is incorrect for the client '{0}'", clientId)));
        }

        var client = new BasicAuthenticationClient
        {
            AuthenticationType = BasicAuthenticationDefaults.AuthenticationScheme,
            IsAuthenticated = true,
            Name = clientId
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(client,
        [
            new Claim(ClaimTypes.Name, clientId)
        ]));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(claimsPrincipal, Scheme.Name)));
    }
}
