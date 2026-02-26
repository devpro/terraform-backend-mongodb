using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Devpro.TerraformBackend.WebApi.Authentication;

public class BasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUserRepository userRepository)
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
        var authBase64Decoded = Encoding.UTF8.GetString(Convert.FromBase64String(
            authorizationHeader.Replace("Basic ", "", StringComparison.OrdinalIgnoreCase)));
        var authSplit = authBase64Decoded.Split([':'], 2);
        if (authSplit.Length != 2)
        {
            return AuthenticateResult.Fail("Invalid Authorization header format");
        }

        var clientId = authSplit[0];
        var clientSecret = authSplit[1];

        // credentials
        var user = await userRepository.CheckAuthentication(clientId, clientSecret);
        if (user == null)
        {
            return AuthenticateResult.Fail("Invalid username or password");
        }

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
