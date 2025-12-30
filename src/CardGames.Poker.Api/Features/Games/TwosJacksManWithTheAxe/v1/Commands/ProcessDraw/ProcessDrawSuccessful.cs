using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.ProcessDraw;

/// <summary>
/// Represents a successful draw action result.
/// </summary>
public record ProcessDrawSuccessful
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public Guid GameId { get; init; }

	/// <summary>
	/// The name of the player who performed the draw action.
	/// </summary>
	public required string PlayerName { get; init; }

	/// <summary>
	/// The cards that were discarded from the player's hand.
	/// </summary>
	public required IReadOnlyCollection<CardInfo> DiscardedCards { get; init; }

	/// <summary>
	/// The new cards dealt to the player to replace discarded cards.
	/// </summary>
	public required IReadOnlyCollection<CardInfo> NewCards { get; init; }

	/// <summary>
	/// Indicates whether all players have completed their draws.
	/// When true, the game has advanced to the second betting round.
	/// </summary>
	public bool DrawComplete { get; init; }

	/// <summary>
	/// The current phase of the game after the draw action.
	/// </summary>
	public required string CurrentPhase { get; init; }

	/// <summary>
	/// The index of the next player to draw, or -1 if the draw phase is complete.
	/// </summary>
	public int NextDrawPlayerIndex { get; init; }

	/// <summary>
	/// The name of the next player to draw, or null if the draw phase is complete.
	/// </summary>
	public string? NextDrawPlayerName { get; init; }
}

/// <summary>
/// Represents a card for the draw result.
/// </summary>
public record CardInfo
{
	/// <summary>
	/// The suit of the card.
	/// </summary>
	public CardSuit Suit { get; init; }

	/// <summary>
	/// The symbol/rank of the card.
	/// </summary>
	public CardSymbol Symbol { get; init; }

	/// <summary>
	/// A display-friendly representation of the card.
	/// </summary>
	public required string Display { get; init; }
}
