using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuctionApp.Api.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuctionApp.Api.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AuctionApp";

    public string Audience { get; set; } = "AuctionAppClient";

    /// <summary>Signing key. Must be at least 32 bytes for HMAC-SHA256.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 720;
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(User user);
}

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateToken(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("displayName", user.DisplayName),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
