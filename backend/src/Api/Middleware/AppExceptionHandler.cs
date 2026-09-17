using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Shared.Exceptions;

namespace Api.Middleware;

/// <summary>
/// Maps AppException subtypes to Problem Details (docs/api/api-conventions.md,
/// docs/PRD.md §29). Anything else is unexpected: logged with full detail
/// here, a generic 500 with no internal detail returned to the client.
/// </summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ConflictAppException => (StatusCodes.Status409Conflict, "Conflict"),
            ForbiddenAppException => (StatusCodes.Status403Forbidden, "Forbidden"),
            ValidationAppException => (StatusCodes.Status400BadRequest, "Validation Failed"),
            _ => (0, string.Empty)
        };

        if (status == 0)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path} (TraceId: {TraceId})",
                httpContext.Request.Method, httpContext.Request.Path, httpContext.TraceIdentifier);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://quickstock.dev/errors/unexpected",
                Extensions = { ["traceId"] = httpContext.TraceIdentifier }
            };
            httpContext.Response.StatusCode = problem.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Type = $"https://quickstock.dev/errors/{title.ToLowerInvariant().Replace(' ', '-')}",
            Extensions = { ["traceId"] = httpContext.TraceIdentifier }
        };

        if (exception is ValidationAppException validationEx)
            details.Extensions["errors"] = validationEx.Errors;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(details, cancellationToken);
        return true;
    }
}
