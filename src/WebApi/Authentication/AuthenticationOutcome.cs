using Devpro.TerraformBackend.Domain.Models;

namespace Devpro.TerraformBackend.WebApi.Authentication;

public enum AuthenticationStatus
{
    Success,
    InvalidCredentials,
    LockedOut
}

/// <summary>
/// The result of one authentication attempt.
/// <para>
/// <see cref="AuthenticationStatus.LockedOut"/> is reported separately from
/// <see cref="AuthenticationStatus.InvalidCredentials"/> so that the two can be logged apart, but both must
/// produce the same <c>401</c> to the caller. Answering a lockout with a distinct status code would tell an
/// attacker which usernames exist and which of their guesses were worth making, which is the enumeration
/// oracle the dummy-hash verify in <c>UserRepository</c> exists to close.
/// </para>
/// </summary>
public sealed record AuthenticationOutcome(AuthenticationStatus Status, UserModel? User)
{
    public static AuthenticationOutcome Success(UserModel user) => new(AuthenticationStatus.Success, user);

    public static AuthenticationOutcome InvalidCredentials { get; } =
        new(AuthenticationStatus.InvalidCredentials, null);

    public static AuthenticationOutcome LockedOut { get; } = new(AuthenticationStatus.LockedOut, null);
}
