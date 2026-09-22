using Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>Rate-limited as a whole (docs/security/security-model.md) —
/// register-store, login, and refresh are exactly the endpoints brute-force
/// / credential-stuffing targets, and there's no legitimate reason for one
/// client to hit any of them more than a handful of times a minute.</summary>
[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(AuthApplicationService authService, IWebHostEnvironment env) : ControllerBase
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

    /// <summary>
    /// No email/SMTP integration exists yet (docs/checkpoints/skills-inventory.md
    /// names this gap explicitly) — a real deployment would send the reset
    /// link by email and never put the token in an HTTP response at all.
    /// In Development only, the token comes back directly so the flow is
    /// actually completable/testable end-to-end without that infrastructure;
    /// in every other environment the response is identical whether or not
    /// the account exists, and the token goes nowhere the client can see.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto, CancellationToken ct)
    {
        var resetHandle = await authService.ForgotPasswordAsync(dto.StoreSlug, dto.Email, ct);
        return Ok(new ForgotPasswordResponse(env.IsDevelopment() ? resetHandle : null));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto, CancellationToken ct)
    {
        await authService.ResetPasswordAsync(dto.Token, dto.NewPassword, ct);
        return NoContent();
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
public sealed record ForgotPasswordDto(string StoreSlug, string Email);
public sealed record ForgotPasswordResponse(string? ResetToken);
public sealed record ResetPasswordDto(string Token, string NewPassword);
