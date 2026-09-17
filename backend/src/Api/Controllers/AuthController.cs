using Application.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(AuthApplicationService authService) : ControllerBase
{
    private const string RefreshCookieName = "refreshToken";

    /// <summary>Creates a new tenant (Store) and its first Admin user. See
    /// docs/PRD.md §5.1. There is no email-verification/marketing funnel in
    /// core scope (docs/PRD.md §3 Non-Goals) — this is an authenticated-later,
    /// direct provisioning endpoint.</summary>
    [HttpPost("register-store")]
    public async Task<ActionResult<RegisterStoreResponse>> RegisterStore(RegisterStoreDto dto, CancellationToken ct)
    {
        var result = await authService.RegisterStoreAsync(
            new RegisterStoreRequest(dto.StoreName, dto.Slug, dto.AdminEmail, dto.AdminPassword, dto.AdminDisplayName), ct);

        return CreatedAtAction(nameof(RegisterStore), new RegisterStoreResponse(result.StoreId, result.Slug, result.AdminUserId));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AccessTokenResponse>> Login(LoginDto dto, CancellationToken ct)
    {
        var tokens = await authService.LoginAsync(new LoginRequest(dto.StoreSlug, dto.Email, dto.Password), ct);
        SetRefreshCookie(tokens.RefreshToken);
        return Ok(new AccessTokenResponse(tokens.AccessToken));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AccessTokenResponse>> Refresh(CancellationToken ct)
    {
        var presentedToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(presentedToken))
            return Unauthorized();

        var tokens = await authService.RefreshAsync(presentedToken, ct);
        SetRefreshCookie(tokens.RefreshToken);
        return Ok(new AccessTokenResponse(tokens.AccessToken));
    }

    private void SetRefreshCookie(string rawToken)
    {
        Response.Cookies.Append(RefreshCookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(14)
        });
    }
}

public sealed record RegisterStoreDto(string StoreName, string Slug, string AdminEmail, string AdminPassword, string AdminDisplayName);
public sealed record RegisterStoreResponse(Guid StoreId, string Slug, Guid AdminUserId);
public sealed record LoginDto(string StoreSlug, string Email, string Password);
public sealed record AccessTokenResponse(string AccessToken);
