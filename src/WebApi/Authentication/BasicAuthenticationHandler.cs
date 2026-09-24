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
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.Fail("Missing Authorization header");
        }

        var authorizationHeader = Request.Headers.Authorization.ToString();

        if (!authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Authorization header does not start with 'Basic'");
        }

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

        var outcome = await credentialAuthenticator.AuthenticateAsync(clientId, clientSecret,
            Context.Connection.RemoteIpAddress);
        if (outcome.Status != AuthenticationStatus.Success || outcome.User is null)
        {
            // the address is logged because it makes a brute force attributable;
            // the username is not, because it is attacker-controlled text,
            // and it holds the password verbatim the day somebody transposes the two fields
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
