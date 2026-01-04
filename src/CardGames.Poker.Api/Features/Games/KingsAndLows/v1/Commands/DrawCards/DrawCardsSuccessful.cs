using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards;

/// <summary>
/// Represents a successful draw action result in Kings and Lows.
/// </summary>
public class DrawCardsSuccessful
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public required Guid GameId { get; init; }

	/// <summary>
	/// The unique identifier of the player who performed the draw action.
	/// </summary>
	public required Guid PlayerId { get; init; }

	/// <summary>
	/// The name of the player who performed the draw action.
	/// </summary>
	public string? PlayerName { get; init; }

	/// <summary>
	/// The number of cards that were discarded.
	/// </summary>
	public required int CardsDiscarded { get; init; }

	/// <summary>
	/// The number of new cards that were drawn.
	/// </summary>
	public required int CardsDrawn { get; init; }

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
	/// </summary>
	public required bool DrawPhaseComplete { get; init; }

	/// <summary>
	/// The next phase of the game after the draw action.
	/// </summary>
	public string? NextPhase { get; init; }

	/// <summary>
	/// The unique identifier of the next player to draw, or null if the draw phase is complete.
	/// </summary>
	public Guid? NextPlayerId { get; init; }

	/// <summary>
	/// The name of the next player to draw, or null if the draw phase is complete.
	/// </summary>
	public string? NextPlayerName { get; init; }
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
