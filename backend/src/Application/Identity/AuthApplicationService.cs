using Application.Common;
using Domain;
using Domain.Identity;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Identity;

/// <summary>
/// Orchestrates the three auth use cases (docs/architecture/authentication-flow.md).
/// Depends only on interfaces — IIdentityService/IJwtTokenService/
/// IRefreshTokenService/IStoreRepository are all implemented in
/// Infrastructure and injected here.
/// </summary>
public sealed class AuthApplicationService(
    IStoreRepository storeRepository,
    IIdentityService identityService,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    ILogger<AuthApplicationService> logger)
{
    public async Task<RegisterStoreResult> RegisterStoreAsync(RegisterStoreRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.StoreName))
            errors["storeName"] = ["Store name is required."];
        if (string.IsNullOrWhiteSpace(request.Slug))
            errors["slug"] = ["Store slug is required."];
        if (string.IsNullOrWhiteSpace(request.AdminEmail))
            errors["adminEmail"] = ["Admin email is required."];
        if (string.IsNullOrWhiteSpace(request.AdminPassword) || request.AdminPassword.Length < 8)
            errors["adminPassword"] = ["Password must be at least 8 characters."];
        if (errors.Count > 0)
            throw new ValidationAppException(errors);

        if (await storeRepository.SlugExistsAsync(request.Slug, ct))
            throw new ConflictAppException($"Store slug '{request.Slug}' is already taken.");

        var store = new Store(request.StoreName, request.Slug);
        await storeRepository.AddAsync(store, ct);

        foreach (var (roleName, permissions) in DefaultRoles.PermissionBundles)
            await identityService.EnsureRoleAsync(store.Id, roleName, permissions, ct);

        var adminUserId = await identityService.CreateUserAsync(
            store.Id, request.AdminEmail, request.AdminPassword, request.AdminDisplayName, [DefaultRoles.Admin], ct);

        await unitOfWork.SaveChangesAsync(ct);

        return new RegisterStoreResult(store.Id, store.Slug, adminUserId);
    }

    public async Task<AuthTokens> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        // Every failure below throws the SAME UnauthorizedAppException with
        // the same default message. An unknown store, an unregistered email,
        // a wrong password and a deactivated account are indistinguishable
        // to the caller by status code, body, or wording — that's the point.
        // Anything that varies between these cases is an oracle someone can
        // use to work out which accounts exist. The reason is logged instead,
        // where an operator can see it and an attacker cannot.
        var store = await storeRepository.GetBySlugAsync(request.StoreSlug, ct);

        // An unknown store still runs a full credential check, against a
        // store id nothing can match. That looks pointless and isn't: it
        // makes an unknown slug cost the same password-hashing work as a
        // real failed login. Short-circuiting here instead answered in
        // ~12ms versus ~400ms, which left store slugs enumerable by
        // response time even though the response bodies are identical.
        var user = await identityService.ValidateCredentialsAsync(
            store?.Id ?? Guid.Empty, request.Email, request.Password, ct);

        if (store is null || user is null || !user.IsActive)
        {
            // Warning, not Information — a failed login is a security-
            // relevant event worth being able to find in logs later (e.g.
            // spotting a credential-stuffing pattern), which is exactly
            // why the email is logged but the password never is, anywhere.
            var reason = store is null ? "unknown store slug"
                : user is null ? "no matching active credentials"
                : "account deactivated";
            logger.LogWarning("Failed login attempt for {Email} in store {StoreSlug} (reason: {Reason})",
                request.Email, request.StoreSlug, reason);
            throw new UnauthorizedAppException();
        }

        var permissions = await identityService.GetPermissionsAsync(user.UserId, ct);
        var accessToken = jwtTokenService.GenerateAccessToken(user.UserId, user.StoreId, permissions);
        var refreshToken = await refreshTokenService.IssueAsync(user.UserId, ct);

        auditWriter.RecordWithExplicitActor(user.StoreId, user.UserId, "auth.login_succeeded", "ApplicationUser", user.UserId.ToString());

        await unitOfWork.SaveChangesAsync(ct);

        return new AuthTokens(accessToken, refreshToken);
    }

    public async Task<AuthTokens> RefreshAsync(string presentedRefreshToken, CancellationToken ct)
    {
        // 401, not 403: a bad refresh token means "we can't establish who you
        // are", which is authentication, not authorization. The client's only
        // correct reaction to any of these is the same — send the user back to
        // log in — so it gains nothing from knowing which one happened, while
        // "token not recognized" vs "already used; session revoked" vs "user is
        // deactivated" would tell whoever holds a stolen token exactly how far
        // they got and whether the account still exists. The specific reason is
        // logged server-side; the response is one flat sentence.
        var rotation = await refreshTokenService.RotateAsync(presentedRefreshToken, ct);
        if (!rotation.Succeeded || rotation.UserId is null)
        {
            logger.LogWarning("Refresh token rotation failed (reason: {Reason})", rotation.FailureReason ?? "unspecified");
            throw new UnauthorizedAppException("Your session has expired. Please sign in again.");
        }

        var user = await identityService.GetUserAsync(rotation.UserId.Value, ct);
        if (user is null || !user.IsActive)
        {
            logger.LogWarning("Refresh rejected for user {UserId} (reason: {Reason})",
                rotation.UserId, user is null ? "user no longer exists" : "account deactivated");
            throw new UnauthorizedAppException("Your session has expired. Please sign in again.");
        }

        var permissions = await identityService.GetPermissionsAsync(user.UserId, ct);
        var accessToken = jwtTokenService.GenerateAccessToken(user.UserId, user.StoreId, permissions);
        await unitOfWork.SaveChangesAsync(ct);

        return new AuthTokens(accessToken, rotation.NewRawToken!);
    }

    /// <summary>
    /// Returns null whether the store doesn't exist, the email isn't
    /// registered, or the account is deactivated — the caller (the
    /// controller) must respond identically either way. This endpoint has
    /// no email delivery to hide behind (no SMTP integration exists yet),
    /// so it's the one place in this codebase where "don't leak whether an
    /// account exists" has to be enforced entirely by the response shape,
    /// not by a side channel.
    /// </summary>
    public async Task<string?> ForgotPasswordAsync(string storeSlug, string email, CancellationToken ct)
    {
        var store = await storeRepository.GetBySlugAsync(storeSlug, ct);
        if (store is null)
            return null;

        var result = await identityService.GeneratePasswordResetTokenAsync(store.Id, email, ct);
        if (result is null)
        {
            logger.LogInformation("Password reset requested for unknown/inactive account {Email} in store {StoreSlug}", email, storeSlug);
            return null;
        }

        var (userId, token) = result.Value;
        logger.LogInformation("Password reset token issued for user {UserId}", userId);
        // userId travels with the token (both opaque to the client) so
        // ResetPasswordAsync doesn't need a second lookup by email.
        return $"{userId}:{token}";
    }

    public async Task ResetPasswordAsync(string resetHandle, string newPassword, CancellationToken ct)
    {
        var parts = resetHandle.Split(':', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var userId))
            throw new ValidationAppException(new Dictionary<string, string[]> { ["token"] = ["Reset token is invalid."] });

        var storeId = await identityService.ResetPasswordAsync(userId, parts[1], newPassword, ct);
        if (storeId is null)
            throw new ValidationAppException(new Dictionary<string, string[]> { ["token"] = ["Reset token is invalid or expired."] });

        auditWriter.RecordWithExplicitActor(storeId.Value, userId, "auth.password_reset_completed", "ApplicationUser", userId.ToString());
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed record RegisterStoreRequest(string StoreName, string Slug, string AdminEmail, string AdminPassword, string AdminDisplayName);
public sealed record RegisterStoreResult(Guid StoreId, string Slug, Guid AdminUserId);
public sealed record LoginRequest(string StoreSlug, string Email, string Password);
public sealed record AuthTokens(string AccessToken, string RefreshToken);
