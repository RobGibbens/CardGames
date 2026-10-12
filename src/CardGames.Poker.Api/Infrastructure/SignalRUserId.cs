using System.Security.Claims;

namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
///     The user id SignalR registers a connection under and later uses for <c>Clients.User</c>.
/// </summary>
public static class SignalRUserId
{
	/// <summary>
	///     Email claim when present, otherwise the subject. Does not read
	///     <c>preferred_username</c> or <see cref="System.Security.Principal.IIdentity.Name" />.
	/// </summary>
	public static string? ForConnection(ClaimsPrincipal? user)
	{
		var email = user?.FindFirstValue(ClaimTypes.Email)
		            ?? user?.FindFirstValue("email");
		if (!string.IsNullOrWhiteSpace(email))
			return email;

		var subject = user?.FindFirstValue(ClaimTypes.NameIdentifier)
		              ?? user?.FindFirstValue("sub");
		return string.IsNullOrWhiteSpace(subject) ? null : subject;
	}

	/// <summary>
	///     Routing key passed to <c>Clients.User</c>. Prefers the id stored from the hub connection.
	/// </summary>
	public static string? RoutingKey(string? signalRUserId, string? email, string? externalId, string? name)
	{
		if (!string.IsNullOrWhiteSpace(signalRUserId))
			return signalRUserId;
		if (!string.IsNullOrWhiteSpace(email))
			return email;
		if (!string.IsNullOrWhiteSpace(externalId))
			return externalId;
		return string.IsNullOrWhiteSpace(name) ? null : name;
	}

	/// <summary>
	///     Chooses the seated player whose identity matches <paramref name="userId" />.
	///     SignalR user id, then email, then external id, then name only when that player
	///     has no email and no stored SignalR user id.
	/// </summary>
	internal static T? SelectPlayer<T>(
		IReadOnlyList<T> players,
		string? userId,
		Func<T, string?> signalRUserId,
		Func<T, string?> email,
		Func<T, string?> externalId,
		Func<T, string?> name) where T : class
	{
		if (string.IsNullOrWhiteSpace(userId) || players.Count == 0)
			return null;

		var bySignalRUserId = Find(players, player => EqualsOrdinal(signalRUserId(player), userId));
		if (bySignalRUserId is not null)
			return bySignalRUserId;

		var byEmail = Find(players, player => EqualsOrdinal(email(player), userId));
		if (byEmail is not null)
			return byEmail;

		var byExternalId = Find(players, player => EqualsOrdinal(externalId(player), userId));
		if (byExternalId is not null)
			return byExternalId;

		return Find(players, player =>
			EqualsOrdinal(name(player), userId) &&
			string.IsNullOrWhiteSpace(email(player)) &&
			string.IsNullOrWhiteSpace(signalRUserId(player)));
	}

	private static T? Find<T>(IReadOnlyList<T> players, Func<T, bool> predicate) where T : class
	{
		foreach (var player in players)
		{
			if (predicate(player))
				return player;
		}

		return null;
	}

	private static bool EqualsOrdinal(string? candidate, string userId) =>
		string.Equals(candidate, userId, StringComparison.Ordinal);
}
