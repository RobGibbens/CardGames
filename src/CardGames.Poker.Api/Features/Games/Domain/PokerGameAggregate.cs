using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.Domain.Events;

namespace CardGames.Poker.Api.Features.Games.Domain;

/// <summary>
/// Aggregate root for a poker game, managing state through event sourcing.
/// Wraps existing game domain models (e.g., FiveCardDrawGame) while providing
/// event-driven state management compatible with Marten.
/// </summary>
public class PokerGameAggregate
{
	// Identity
	public Guid Id { get; private set; }

	// State
	public GameType GameType { get; private set; }
	public GameStatus Status { get; private set; }
	public GameConfiguration Configuration { get; private set; }
	public DateTime CreatedAt { get; private set; }

	// Players
	public List<GamePlayer> Players { get; private set; } = [];

	// Marten requires a default constructor
	public PokerGameAggregate() { }

	/// <summary>
	/// Apply method for GameCreated event (Marten convention).
	/// </summary>
	public void Apply(GameCreated @event)
	{
		Id = @event.GameId;
		GameType = @event.GameType;
		Configuration = @event.Configuration;
		Status = GameStatus.WaitingForPlayers;
		CreatedAt = @event.CreatedAt;
	}

	/// <summary>
	/// Apply method for PlayerJoined event (Marten convention).
	/// </summary>
	public void Apply(PlayerJoined @event)
	{
		Players.Add(new GamePlayer(
			@event.PlayerId,
			@event.PlayerName,
			@event.BuyIn,
			@event.Position
		));

		// Update status if we have minimum players
		if (Players.Count >= 2)
		{
			Status = GameStatus.ReadyToStart;
		}
	}

	/// <summary>
	/// Check if a player can join the game.
	/// </summary>
	public bool CanPlayerJoin()
	{
		return Status == GameStatus.WaitingForPlayers || Status == GameStatus.ReadyToStart;
	}

	/// <summary>
	/// Check if the game is at maximum capacity.
	/// </summary>
	public bool IsFull()
	{
		return Players.Count >= Configuration.MaxPlayers;
	}

	/// <summary>
	/// Get the next available seat position.
	/// </summary>
	public int GetNextPosition()
	{
		return Players.Count;
	}
}