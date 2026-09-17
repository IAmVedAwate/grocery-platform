namespace Application.Identity;

/// <summary>
/// Rotating refresh tokens with reuse detection (docs/decisions/ADR-004,
/// docs/architecture/authentication-flow.md). Raw token values are only
/// ever handled here and at the Api boundary — everywhere else only the
/// hash is persisted.
/// </summary>
public interface IRefreshTokenService
{
    Task<string> IssueAsync(Guid userId, CancellationToken ct);

    Task<RefreshRotationResult> RotateAsync(string presentedRawToken, CancellationToken ct);
}

public sealed record RefreshRotationResult(bool Succeeded, Guid? UserId, string? NewRawToken, string? FailureReason)
{
    public static RefreshRotationResult Success(Guid userId, string newRawToken) => new(true, userId, newRawToken, null);
    public static RefreshRotationResult Failure(string reason) => new(false, null, null, reason);
}
