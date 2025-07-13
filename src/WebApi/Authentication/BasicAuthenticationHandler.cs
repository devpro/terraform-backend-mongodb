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
        // raises an error if no authorization header
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));
        }

        var authorizationHeader = Request.Headers.Authorization.ToString();

        // raises an error if the authorization header is not Basic
        if (!authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Authorization header does not start with 'Basic'"));
        }

        // decrypts the authorization header and split out the client id/secret which is separated by the first ':'
        var authBase64Decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorizationHeader.Replace("Basic ", "", StringComparison.OrdinalIgnoreCase)));
        var authSplit = authBase64Decoded.Split([':'], 2);

        // sends an error if no username and password
        if (authSplit.Length != 2)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header format"));
        }

        // stores the client ID and secret
        var clientId = authSplit[0];
        var clientSecret = authSplit[1];

        // TODO: store this info in the database & restrict a user to its organization
        // checkClient ID and secret are incorrect
        if (clientId != "admin" || clientSecret != "admin")
        {
            return Task.FromResult(AuthenticateResult.Fail(string.Format("The secret is incorrect for the client '{0}'", clientId)));
        }

        // authenicates the client using basic authentication
        var client = new BasicAuthenticationClient
        {
            AuthenticationType = BasicAuthenticationDefaults.AuthenticationScheme,
            IsAuthenticated = true,
            Name = clientId
        };

        // set the client ID as the name claim type
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(client,
        [
            new Claim(ClaimTypes.Name, clientId)
        ]));

        // returns a success result
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(claimsPrincipal, Scheme.Name)));
    }
}
