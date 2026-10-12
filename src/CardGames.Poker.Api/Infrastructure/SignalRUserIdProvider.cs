using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
///     Maps a SignalR connection to the user id <c>Clients.User</c> routes on.
/// </summary>
public sealed class SignalRUserIdProvider : IUserIdProvider
{
	/// <summary>
	///     Returns the user id for SignalR routing.
	/// </summary>
	public string? GetUserId(HubConnectionContext connection)
	{
		return SignalRUserId.ForConnection(connection.User);
	}
}