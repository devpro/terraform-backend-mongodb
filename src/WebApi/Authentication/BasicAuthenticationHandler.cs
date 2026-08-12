using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;

namespace Devpro.TerraformBackend.WebApi.Authentication;

public class BasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ICredentialAuthenticator credentialAuthenticator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // workaround as it seems impossible to prevent (from Startup)
        var path = Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.StartsWith("/scalar/") ||
            path.StartsWith("/openapi/"))
        {
            return AuthenticateResult.NoResult();
        }

        // checks authorization header
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.Fail("Missing Authorization header");
        }

        var authorizationHeader = Request.Headers.Authorization.ToString();

        // checks authorization header starts with Basic
        if (!authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Authorization header does not start with 'Basic'");
        }

        // decrypts the authorization header and split out the client id/secret
        string authBase64Decoded;
        try
        {
            authBase64Decoded = Encoding.UTF8.GetString(Convert.FromBase64String(
                authorizationHeader.Replace("Basic ", "", StringComparison.OrdinalIgnoreCase)));
        }
        catch (FormatException)
        {
            return AuthenticateResult.Fail("Authorization header is not valid Base64");
        }

        var authSplit = authBase64Decoded.Split([':'], 2);
        if (authSplit.Length != 2)
        {
            return AuthenticateResult.Fail("Invalid Authorization header format");
        }

        var clientId = authSplit[0];
        var clientSecret = authSplit[1];

        // credentials
        var outcome = await credentialAuthenticator.AuthenticateAsync(clientId, clientSecret,
            Context.Connection.RemoteIpAddress);
        if (outcome.Status != AuthenticationStatus.Success || outcome.User is null)
        {
            // The source address is logged and the supplied username is not. The address is what makes a
            // brute force detectable and attributable, and it is the half that was missing: a run of 361
            // failures against this API recorded not one of them. The username is attacker-controlled text
            // that would fill the log with whatever was sent, and would capture a password verbatim the day
            // somebody transposes the two fields.
            Logger.LogWarning("Authentication failed from {RemoteAddress} with status {Status}",
                Context.Connection.RemoteIpAddress?.ToString() ?? "an unknown address", outcome.Status);
            return AuthenticateResult.Fail("Invalid username or password");
        }

        var user = outcome.User;
        var client = new BasicAuthenticationClient
        {
            AuthenticationType = BasicAuthenticationClient.AuthenticationScheme,
            IsAuthenticated = true,
            Name = user.Username
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(client,
        [
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimsPrincipalExtensions.Tenant, user.Tenant)
        ]));

        return AuthenticateResult.Success(new AuthenticationTicket(claimsPrincipal, Scheme.Name));
    }
}
