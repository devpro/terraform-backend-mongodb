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
/// <see cref="AuthenticationStatus.LockedOut"/> is distinct from <see cref="AuthenticationStatus.InvalidCredentials"/> so that the two are logged apart,
/// but both answer the same <c>401</c>: a distinct status would tell an attacker which usernames exist, the oracle <c>UserRepository</c> closes.
/// </para>
/// </summary>
public sealed record AuthenticationOutcome(AuthenticationStatus Status, UserModel? User)
{
    public static AuthenticationOutcome Success(UserModel user) => new(AuthenticationStatus.Success, user);

    public static AuthenticationOutcome InvalidCredentials { get; } =
        new(AuthenticationStatus.InvalidCredentials, null);

    public static AuthenticationOutcome LockedOut { get; } = new(AuthenticationStatus.LockedOut, null);
}
