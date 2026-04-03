using CardGames.Poker.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
/// Maps SignalR user identity to the application's stable user id.
/// Delegates to <see cref="IGameUserIdResolver"/> so that SignalR routing keys
/// match the broadcast cache and private-state dictionary keys exactly.
/// </summary>
public sealed class SignalRUserIdProvider(IGameUserIdResolver resolver) : IUserIdProvider
{
    /// <summary>
    /// Returns the user id for SignalR routing.
    /// </summary>
    public string? GetUserId(HubConnectionContext connection)
        => resolver.ResolveFromClaims(connection.User);
}
