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
/// The cache removes that cost for a credential already verified, and the lockout caps the attempts of one
/// that is not.
/// </para>
/// <para>
/// The lockout lives in MongoDB so that it holds across every replica.
/// The cache stays in process: a miss on another replica costs one more verify and weakens nothing.
/// </para>
/// <para>
/// Nothing here takes the request's cancellation token, so a caller that disconnects after a wrong guess is
/// still counted.
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

        var failures = await lockoutRepository.GetFailureCountAsync(username, addressKey);
        if (failures >= configuration.MaxFailedAttempts)
        {
            logger.LogWarning(
                "Authentication refused: too many consecutive failures from {RemoteAddress} for a known account",
                addressKey);
            return AuthenticationOutcome.LockedOut;
        }

        // only successful verifications are cached, so a wrong password always pays the BCrypt cost and always
        // reaches the counter below: the cache must never become a way to skip the lockout
        var credentialKey = CredentialKey(username, password);
        if (cache.TryGetValue<UserModel>(credentialKey, out var cachedUser) && cachedUser is not null)
        {
            return AuthenticationOutcome.Success(cachedUser);
        }

        var user = await userRepository.CheckAuthentication(username, password);
        if (user is null)
        {
            await RecordFailureAsync(username, addressKey);
            return AuthenticationOutcome.InvalidCredentials;
        }

        await lockoutRepository.ClearAsync(username, addressKey);

        if (configuration.CredentialCacheDuration > TimeSpan.Zero)
        {
            cache.Set(credentialKey, user, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = configuration.CredentialCacheDuration,
                Size = 1
            });
        }

        return AuthenticationOutcome.Success(user);
    }

    private static string CredentialKey(string username, string password)
    {
        var hash = HMACSHA256.HashData(CacheKeySalt, Encoding.UTF8.GetBytes($"{username} {password}"));
        return $"credential:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Records one failure against the username and the caller's address together.
    /// <para>
    /// A lockout on the username alone is a denial of service anybody can trigger:
    /// guessing at <c>admin</c> from anywhere would lock the real operator out.
    /// An attacker spread across many addresses gets the same allowance per address,
    /// and that volume is for the ingress rate limit to stop.
    /// </para>
    /// </summary>
    private async Task RecordFailureAsync(string username, string addressKey)
    {
        var failures = await lockoutRepository.RecordFailureAsync(username, addressKey, configuration.LockoutDuration);
        if (failures == configuration.MaxFailedAttempts)
        {
            logger.LogWarning(
                "Authentication locked out for {LockoutDuration} after {MaxFailedAttempts} consecutive failures from {RemoteAddress}",
                configuration.LockoutDuration, configuration.MaxFailedAttempts, addressKey);
        }
    }

    private static string Describe(IPAddress? remoteAddress) => remoteAddress?.ToString() ?? "unknown";
}
