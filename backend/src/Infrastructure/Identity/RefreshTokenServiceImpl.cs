using System.Security.Cryptography;
using System.Text;
using Application.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Identity;

/// <summary>
/// Rotation + reuse detection (docs/decisions/ADR-004). Does not call
/// SaveChanges itself — it stages changes on the shared DbContext, and the
/// caller (Application.Identity.AuthApplicationService) commits via
/// IUnitOfWork once per use case, alongside whatever else changed.
/// </summary>
public sealed class RefreshTokenServiceImpl(GroceryDbContext db, IConfiguration configuration) : IRefreshTokenService
{
    public async Task<string> IssueAsync(Guid userId, CancellationToken ct)
    {
        var (rawToken, _) = await CreateTokenAsync(userId, Guid.NewGuid(), ct);
        return rawToken;
    }

    public async Task<RefreshRotationResult> RotateAsync(string presentedRawToken, CancellationToken ct)
    {
        var hash = Hash(presentedRawToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null)
            return RefreshRotationResult.Failure("Refresh token not recognized.");

        if (existing.RevokedAtUtc is not null)
        {
            // Reuse of an already-rotated token: someone is replaying a
            // captured token. Revoke the entire family, not just this one.
            await RevokeFamilyAsync(existing.FamilyId, ct);
            return RefreshRotationResult.Failure("Refresh token has already been used; session revoked.");
        }

        if (existing.ExpiresAtUtc < DateTime.UtcNow)
            return RefreshRotationResult.Failure("Refresh token has expired.");

        var (newRawToken, newEntity) = await CreateTokenAsync(existing.UserId, existing.FamilyId, ct);
        existing.RevokedAtUtc = DateTime.UtcNow;
        existing.ReplacedByTokenId = newEntity.Id;

        return RefreshRotationResult.Success(existing.UserId, newRawToken);
    }

    private async Task<(string RawToken, Infrastructure.Identity.RefreshTokenEntity Entity)> CreateTokenAsync(Guid userId, Guid familyId, CancellationToken ct)
    {
        var rawToken = GenerateRawToken();
        var days = int.TryParse(configuration["JWT_REFRESH_TOKEN_DAYS"], out var d) ? d : 14;
        var entity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(rawToken),
            FamilyId = familyId,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(days),
            CreatedAtUtc = DateTime.UtcNow
        };
        await db.RefreshTokens.AddAsync(entity, ct);
        return (rawToken, entity);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken ct)
    {
        var activeTokens = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var token in activeTokens)
            token.RevokedAtUtc = DateTime.UtcNow;
    }

    private static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
