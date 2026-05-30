using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CardGames.Poker.Web.Infrastructure;

public sealed class InternalApiUserTokenFactory(IOptions<InternalApiAuthOptions> authOptions)
{
    public string? CreateToken(ClaimsPrincipal user)
    {
        if (user.Identity is not { IsAuthenticated: true })
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var userEmail = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email");

        var userName = userEmail
            ?? user.FindFirstValue("preferred_username")
            ?? user.Identity.Name;

        userEmail ??= !string.IsNullOrWhiteSpace(userName) && userName.Contains('@')
            ? userName
            : null;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId)
        };

        if (!string.IsNullOrWhiteSpace(userName))
        {
            claims.Add(new Claim(ClaimTypes.Name, userName));
            claims.Add(new Claim("preferred_username", userName));
        }

        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            claims.Add(new Claim(ClaimTypes.Email, userEmail));
            claims.Add(new Claim("email", userEmail));
        }

        var options = authOptions.Value;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(options.TokenLifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}