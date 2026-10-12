namespace CardGames.Poker.Api.Services;

/// <summary>
///     Remembers which SignalR connection ids are seated as a given player.
///     Connection ids are <c>HubCallerContext.ConnectionId</c>, not user ids or player names.
/// </summary>
public interface IGameConnectionRegistry
{
	/// <summary>
	///     Records that <paramref name="connectionId" /> is seated as <paramref name="playerId" /> in
	///     <paramref name="gameId" />. A connection already recorded for another seat is removed from that seat.
	/// </summary>
	void Add(Guid gameId, Guid playerId, string connectionId);

	/// <summary>
	///     Drops <paramref name="connectionId" /> from every seat it was recorded under.
	/// </summary>
	void Remove(string connectionId);

	/// <summary>
	///     Returns the connection ids currently recorded for the seated player.
	///     An unknown pair returns an empty list.
	/// </summary>
	IReadOnlyList<string> GetConnectionIds(Guid gameId, Guid playerId);
}
