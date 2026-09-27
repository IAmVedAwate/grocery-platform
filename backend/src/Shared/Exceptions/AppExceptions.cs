namespace Shared.Exceptions;

/// <summary>
/// Base type for exceptions the Api layer's exception-handling middleware
/// maps to a Problem Details response (docs/api/api-conventions.md,
/// docs/PRD.md §29). Anything NOT derived from this is treated as
/// unexpected and returns a generic 500 with full detail only in the logs.
/// </summary>
public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string entityName, object key)
    : AppException($"{entityName} '{key}' was not found.");

public sealed class ValidationAppException(IReadOnlyDictionary<string, string[]> errors)
    : AppException("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class ConflictAppException(string message) : AppException(message);

public sealed class ForbiddenAppException(string message = "You do not have permission to perform this action.")
    : AppException(message);

/// <summary>
/// Authentication failed: the caller is not who they claim to be, or
/// isn't identified at all — 401, not 403 (which means "we know who you
/// are, and you still may not do this") and not 404 (which answers a
/// question about whether a resource exists that an unauthenticated
/// caller has no business getting an answer to).
///
/// The default message is deliberately the only thing every failure mode
/// says. Whether the store slug is wrong, the email is unregistered, the
/// password is wrong, or the account is deactivated, the caller gets one
/// identical sentence — anything more specific is an account-enumeration
/// oracle. The distinguishing detail belongs in the server-side log,
/// where an operator can see it and an attacker cannot.
/// </summary>
public sealed class UnauthorizedAppException(string message = "Invalid credentials.")
    : AppException(message);

/// <summary>
/// A dependency we don't control is unavailable or overloaded — 503, and
/// the one error class where "try again in a moment" is genuinely the
/// right advice to give the user.
///
/// Distinct from a 500: nothing is broken here and there's no bug to
/// investigate. Collapsing the two would both alarm the user and bury a
/// real defect in the same bucket as a transient upstream hiccup.
/// </summary>
public sealed class ServiceUnavailableAppException(string message)
    : AppException(message);
