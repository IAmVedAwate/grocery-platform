namespace Infrastructure.Identity;

/// <summary>
/// Only the HASH is ever persisted (docs/decisions/ADR-004). FamilyId
/// links every token descended from one login so reuse of an
/// already-rotated token revokes the whole family.
/// </summary>
public class RefreshTokenEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public Guid FamilyId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
