using System.Security.Claims;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Centralised user-id resolution for SignalR routing and broadcast cache keying.
/// Registered as a singleton because it is stateless.
/// </summary>
public sealed class GameUserIdResolver : IGameUserIdResolver
{
    /// <inheritdoc />
    public string? ResolveFromClaims(ClaimsPrincipal? user)
    {
        if (user is null) return null;

        return user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email")
            ?? user.FindFirstValue("preferred_username")
            ?? user.Identity?.Name;
    }

    /// <inheritdoc />
    public string? ResolveFromPlayer(string? email, string? name, string? externalId)
    {
        // If email has multiple @ symbols it is malformed (e.g. "user@example.com@localhost")
        // — prefer Name which is typically the clean email.
        var isMalformedEmail = !string.IsNullOrWhiteSpace(email) && email!.Count(c => c == '@') > 1;
        if (isMalformedEmail && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return email ?? name ?? externalId;
    }
}
