using System.Net;
using System.Security.Cryptography;
using System.Text;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace Devpro.TerraformBackend.WebApi.Authentication;

/// <summary>
/// Verifies credentials with a short-lived cache in front of BCrypt and a lockout behind it.
/// <para>
/// The Terraform <c>http</c> backend sends a Basic credential on every request and supports no token,
/// so without the cache every operation pays a BCrypt verify, about 139 ms of CPU at work factor 10.
/// The cache removes that cost for a credential already verified, and the lockout caps the attempts of one that is not.
/// The user itself is read on every request, so a change of tenant, a deletion or a new password applies at once.
/// </para>
/// <para>
/// The lockout lives in MongoDB so that it holds across every replica.
/// The cache stays in process: a miss on another replica costs one more verify and weakens nothing.
/// </para>
/// <para>
/// Nothing here takes the request's cancellation token, so a caller that disconnects after a wrong guess is still counted.
/// </para>
/// </summary>
public sealed class ThrottledCredentialAuthenticator(
    IUserRepository userRepository,
    ILockoutRepository lockoutRepository,
    IMemoryCache cache,
    ApplicationConfiguration configuration,
    ILogger<ThrottledCredentialAuthenticator> logger)
    : ICredentialAuthenticator
{
    /// <summary>
    /// Keys the credential cache without holding the credential.
    /// Generated per process, because a fixed salt would let identical deployments share precomputable keys.
    /// </summary>
    private static readonly byte[] CacheKeySalt = RandomNumberGenerator.GetBytes(32);

    public async Task<AuthenticationOutcome> AuthenticateAsync(string username, string password, IPAddress? remoteAddress)
    {
        var addressKey = Describe(remoteAddress);

        // the cache holds the hash the password was verified against, never the user:
        // a cached user would keep its tenant after a rename or a move, and stay valid after a deletion, until the entry expires
        var credentialKey = CredentialKey(username, password);
        cache.TryGetValue<string>(credentialKey, out var verifiedHash);

        // a hit reads the user together with the counter, in the one request the counter alone costs:
        // the lockout is checked before a hit is accepted, or a locked-out caller could test guesses against the cache without limit
        int failures;
        UserModel? current = null;
        if (verifiedHash is not null)
        {
            (failures, current) = await lockoutRepository.GetFailureCountAndUserAsync(username, addressKey);
        }
        else
        {
            failures = await lockoutRepository.GetFailureCountAsync(username, addressKey);
        }

        if (failures >= configuration.MaxFailedAttempts)
        {
            logger.LogWarning(
                "Authentication refused: too many consecutive failures from {RemoteAddress} for a known account",
                addressKey);
            return AuthenticationOutcome.LockedOut;
        }

        // only successful verifications are cached, so a wrong password always pays the BCrypt cost and always reaches the counter below:
        // the cache must never become a way to skip the lockout
        // a user deleted, or a hash changed since the verify, sends the credential through BCrypt again
        if (verifiedHash is not null)
        {
            if (current is not null && current.PasswordHash == verifiedHash)
            {
                return AuthenticationOutcome.Success(current);
            }

            cache.Remove(credentialKey);
        }

        // the attempt is counted before it is verified, and the counter cleared on success, so that guesses sent together are each checked against a count that includes the others:
        // counted after the verify, every guess in flight passes the check above with the same stale count, and a parallel burst gets far more than its allowance
        var attempt = await lockoutRepository.RecordFailureAsync(username, addressKey, configuration.LockoutDuration);
        if (attempt > configuration.MaxFailedAttempts)
        {
            logger.LogWarning(
                "Authentication refused: too many consecutive failures from {RemoteAddress} for a known account",
                addressKey);
            return AuthenticationOutcome.LockedOut;
        }

        var user = await userRepository.CheckAuthentication(username, password);
        if (user is null)
        {
            if (attempt == configuration.MaxFailedAttempts)
            {
                logger.LogWarning(
                    "Authentication locked out for {LockoutDuration} after {MaxFailedAttempts} consecutive failures from {RemoteAddress}",
                    configuration.LockoutDuration, configuration.MaxFailedAttempts, addressKey);
            }

            return AuthenticationOutcome.InvalidCredentials;
        }

        await lockoutRepository.ClearAsync(username, addressKey);

        if (configuration.CredentialCacheDuration > TimeSpan.Zero)
        {
            cache.Set(credentialKey, user.PasswordHash, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = configuration.CredentialCacheDuration,
                Size = 1
            });
        }

        return AuthenticationOutcome.Success(user);
    }

    /// <summary>
    /// Prefixes the username with its length, since any separator could also appear in either field,
    /// and an ambiguous join lets two different credentials share one key.
    /// </summary>
    private static string CredentialKey(string username, string password)
    {
        var hash = HMACSHA256.HashData(CacheKeySalt, Encoding.UTF8.GetBytes($"{username.Length}:{username}{password}"));
        return $"credential:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Scopes the failure counter to the username and the caller's address together.
    /// <para>
    /// A lockout on the username alone is a denial of service anybody can trigger: guessing at <c>admin</c> from anywhere would lock the real operator out.
    /// An attacker spread across many addresses gets the same allowance per address, and that volume is for the ingress rate limit to stop.
    /// </para>
    /// </summary>
    private static string Describe(IPAddress? remoteAddress) => remoteAddress?.ToString() ?? "unknown";
}
