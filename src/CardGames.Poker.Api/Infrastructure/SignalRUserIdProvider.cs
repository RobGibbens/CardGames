using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
/// Maps SignalR user identity to the application's stable user id.
/// Uses the email claim to match how players are stored in the database (by name/email).
/// This enables <c>Clients.User(userId)</c> routing for header-authenticated SignalR connections.
/// </summary>
public sealed class SignalRUserIdProvider : IUserIdProvider
{
    /// <summary>
    /// Returns the user id for SignalR routing. Prefers email claim to match player lookup.
    /// </summary>
    public string? GetUserId(HubConnectionContext connection)
    {
        var user = connection.User;
        // Use email to align with how players are stored/matched in the database.
        return user?.FindFirstValue(ClaimTypes.Email)
            ?? user?.FindFirstValue("email")
            ?? user?.FindFirstValue("preferred_username")
            ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
