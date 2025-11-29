using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CardGames.Poker.Api.Features.Auth;

public interface ITokenService
{
    string GenerateToken(UserRecord user);
    DateTime GetExpiration();
}

public class JwtTokenService : ITokenService
{
    private readonly AuthSettings _settings;

    public JwtTokenService(IOptions<AuthSettings> settings)
    {
        _settings = settings.Value;
    }

    public string GenerateToken(UserRecord user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("display_name", user.DisplayName ?? user.Email),
            new Claim("auth_provider", user.AuthProvider ?? "local")
        };

        var token = new JwtSecurityToken(
            issuer: _settings.JwtIssuer,
            audience: _settings.JwtAudience,
            claims: claims,
            expires: GetExpiration(),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public DateTime GetExpiration()
    {
        return DateTime.UtcNow.AddMinutes(_settings.JwtExpirationMinutes);
    }
}
