using System.Net;

namespace Devpro.TerraformBackend.WebApi.Authentication;

public interface ICredentialAuthenticator
{
    /// <summary>
    /// Verifies a credential, applying the lockout and the short-lived cache around it.
    /// </summary>
    /// <param name="username">The supplied username, which is attacker-controlled until it is found.</param>
    /// <param name="password">The supplied password.</param>
    /// <param name="remoteAddress">
    /// The caller's address, used to scope the lockout.
    /// Null when it cannot be determined, in which case the lockout falls back to counting against the username alone.
    /// </param>
    Task<AuthenticationOutcome> AuthenticateAsync(string username, string password, IPAddress? remoteAddress);
}
