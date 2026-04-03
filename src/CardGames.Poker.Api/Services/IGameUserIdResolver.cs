using System.Security.Claims;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Single source of truth for resolving user identifiers used in SignalR routing
/// and broadcast cache keying. All paths that derive a "user id" for game state
/// delivery must use this service to ensure cache keys match SignalR routing exactly.
/// </summary>
public interface IGameUserIdResolver
{
    /// <summary>
    /// Resolves a stable user identifier from a <see cref="ClaimsPrincipal"/>.
    /// Used by SignalR user id provider, hub methods, and HTTP-context identity resolution.
    /// The claim priority is: Email → "email" → "preferred_username" → Identity.Name.
    /// </summary>
    string? ResolveFromClaims(ClaimsPrincipal? user);

    /// <summary>
    /// Resolves a stable user identifier from player entity fields stored in the database.
    /// Handles malformed emails (multiple @ symbols) by preferring Name over Email.
    /// The result must match what <see cref="ResolveFromClaims"/> would produce for the same user.
    /// </summary>
    string? ResolveFromPlayer(string? email, string? name, string? externalId);
}
