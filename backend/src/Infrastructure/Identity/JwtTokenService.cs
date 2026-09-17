using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Identity;

public sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string GenerateAccessToken(Guid userId, Guid storeId, IEnumerable<string> permissions)
    {
        var signingKey = configuration["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY is not configured.");
        var issuer = configuration["JWT_ISSUER"] ?? "quickstock-api";
        var audience = configuration["JWT_AUDIENCE"] ?? "quickstock-client";
        var minutes = int.TryParse(configuration["JWT_ACCESS_TOKEN_MINUTES"], out var m) ? m : 15;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(HttpTenantContext.StoreIdClaimType, storeId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
