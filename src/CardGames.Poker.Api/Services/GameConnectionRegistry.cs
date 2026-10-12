using System.Collections.Concurrent;

namespace CardGames.Poker.Api.Services;

/// <summary>
///     In-memory map of seated players to their live hub connection ids.
/// </summary>
public sealed class GameConnectionRegistry : IGameConnectionRegistry
{
	private readonly ConcurrentDictionary<(Guid GameId, Guid PlayerId), ConcurrentDictionary<string, byte>>
		_connectionsByPlayer = new();

	private readonly ConcurrentDictionary<string, (Guid GameId, Guid PlayerId)> _ownerByConnection =
		new(StringComparer.Ordinal);

	/// <inheritdoc />
	public void Add(Guid gameId, Guid playerId, string connectionId)
	{
		var next = (gameId, playerId);
		if (_ownerByConnection.TryGetValue(connectionId, out var previous) && previous != next)
			RemoveFromPlayer(previous, connectionId);

		_ownerByConnection[connectionId] = next;
		var connections = _connectionsByPlayer.GetOrAdd(
			next,
			_ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
		connections[connectionId] = 0;
	}

	/// <inheritdoc />
	public void Remove(string connectionId)
	{
		if (!_ownerByConnection.TryRemove(connectionId, out var owner))
			return;

		RemoveFromPlayer(owner, connectionId);
	}

	/// <inheritdoc />
	public IReadOnlyList<string> GetConnectionIds(Guid gameId, Guid playerId)
	{
		if (!_connectionsByPlayer.TryGetValue((gameId, playerId), out var connections))
			return [];

		return connections.Keys.ToArray();
	}

	private void RemoveFromPlayer((Guid GameId, Guid PlayerId) owner, string connectionId)
	{
		if (!_connectionsByPlayer.TryGetValue(owner, out var connections))
			return;

		connections.TryRemove(connectionId, out _);
		if (connections.IsEmpty)
		{
			_connectionsByPlayer.TryRemove(
				new KeyValuePair<(Guid GameId, Guid PlayerId), ConcurrentDictionary<string, byte>>(owner, connections));
		}
	}
}
