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
