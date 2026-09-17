namespace Application.Common;

/// <summary>
/// Wraps a single SaveChanges boundary so a use case spanning multiple
/// repositories commits atomically. Needed from Phase 2 onward for the
/// checkout transaction (docs/PRD.md §22); introduced now so that work
/// doesn't require re-plumbing the repository layer later.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
