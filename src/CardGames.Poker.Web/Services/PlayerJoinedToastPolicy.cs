using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Web.Services;

/// <summary>
///     Decides whether a <see cref="PlayerJoinedDto" /> describes the viewer, who should not toast their own join.
/// </summary>
public static class PlayerJoinedToastPolicy
{
	public static bool IsOwnJoin(
		PlayerJoinedDto notification,
		Guid? currentPlayerId,
		string? currentPlayerName,
		string? loggedInEmail)
	{
		if (notification.PlayerId != Guid.Empty
		    && currentPlayerId is Guid viewerId
		    && viewerId == notification.PlayerId)
			return true;

		if (string.IsNullOrWhiteSpace(notification.PlayerName))
			return false;

		if (!string.IsNullOrWhiteSpace(currentPlayerName)
		    && string.Equals(notification.PlayerName, currentPlayerName, StringComparison.OrdinalIgnoreCase))
			return true;

		if (!string.IsNullOrWhiteSpace(loggedInEmail)
		    && string.Equals(notification.PlayerName, loggedInEmail, StringComparison.OrdinalIgnoreCase))
			return true;

		return false;
	}
}
