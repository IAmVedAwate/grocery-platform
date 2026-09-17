namespace Application.Identity;

/// <summary>
/// Issues short-lived JWT access tokens (docs/architecture/authentication-flow.md).
/// Every token carries sub (user id), store_id (tenant), and a permissions
/// claim resolved at issue time.
/// </summary>
public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, Guid storeId, IEnumerable<string> permissions);
}
