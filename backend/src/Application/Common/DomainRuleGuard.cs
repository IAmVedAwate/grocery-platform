using Shared.Exceptions;

namespace Application.Common;

/// <summary>
/// Domain entities throw InvalidOperationException for invalid state
/// transitions (e.g. PurchaseOrder.Approve before Submit) — a genuinely
/// expected, client-facing condition, not a server fault. AppExceptionHandler
/// only maps Shared.Exceptions.AppException subtypes, so an untranslated
/// InvalidOperationException falls through to a generic 500. This wraps
/// exactly those calls and rethrows as ConflictAppException (409) — never
/// applied blanket in the exception handler itself, because
/// InvalidOperationException is also thrown for genuine server-side bugs
/// elsewhere (e.g. HttpTenantContext's missing-claim guard), which really
/// should be a 500.
/// </summary>
public static class DomainRuleGuard
{
    public static void Run(Action action)
    {
        try { action(); }
        catch (InvalidOperationException ex) { throw new ConflictAppException(ex.Message); }
    }

    public static T Run<T>(Func<T> action)
    {
        try { return action(); }
        catch (InvalidOperationException ex) { throw new ConflictAppException(ex.Message); }
    }
}
