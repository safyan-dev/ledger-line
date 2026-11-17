using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PaymentReconciliation.Modules.Identity.Domain;

namespace PaymentReconciliation.Modules.Identity.Application;

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IJwtTokenService
{
    IssuedAccessToken CreateAccessToken(ApplicationUser user);
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public IssuedAccessToken CreateAccessToken(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(Math.Max(1, _options.AccessTokenMinutes));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("display_name", user.DisplayName),
            new("platform_admin", user.IsPlatformAdmin.ToString().ToLowerInvariant()),
            new("security_stamp", user.SecurityStamp ?? string.Empty)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
