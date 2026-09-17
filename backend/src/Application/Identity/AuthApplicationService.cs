using Application.Common;
using Domain;
using Domain.Identity;
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
    IUnitOfWork unitOfWork)
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
        var store = await storeRepository.GetBySlugAsync(request.StoreSlug, ct)
            ?? throw new NotFoundException(nameof(Store), request.StoreSlug);

        var user = await identityService.ValidateCredentialsAsync(store.Id, request.Email, request.Password, ct);
        if (user is null || !user.IsActive)
            throw new NotFoundException("User", request.Email); // deliberately generic — see security-model.md

        var permissions = await identityService.GetPermissionsAsync(user.UserId, ct);
        var accessToken = jwtTokenService.GenerateAccessToken(user.UserId, user.StoreId, permissions);
        var refreshToken = await refreshTokenService.IssueAsync(user.UserId, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new AuthTokens(accessToken, refreshToken);
    }

    public async Task<AuthTokens> RefreshAsync(string presentedRefreshToken, CancellationToken ct)
    {
        var rotation = await refreshTokenService.RotateAsync(presentedRefreshToken, ct);
        if (!rotation.Succeeded || rotation.UserId is null)
            throw new ForbiddenAppException(rotation.FailureReason ?? "Refresh token is invalid.");

        var user = await identityService.GetUserAsync(rotation.UserId.Value, ct)
            ?? throw new ForbiddenAppException("User no longer exists.");
        if (!user.IsActive)
            throw new ForbiddenAppException("User is deactivated.");

        var permissions = await identityService.GetPermissionsAsync(user.UserId, ct);
        var accessToken = jwtTokenService.GenerateAccessToken(user.UserId, user.StoreId, permissions);
        await unitOfWork.SaveChangesAsync(ct);

        return new AuthTokens(accessToken, rotation.NewRawToken!);
    }
}

public sealed record RegisterStoreRequest(string StoreName, string Slug, string AdminEmail, string AdminPassword, string AdminDisplayName);
public sealed record RegisterStoreResult(Guid StoreId, string Slug, Guid AdminUserId);
public sealed record LoginRequest(string StoreSlug, string Email, string Password);
public sealed record AuthTokens(string AccessToken, string RefreshToken);
