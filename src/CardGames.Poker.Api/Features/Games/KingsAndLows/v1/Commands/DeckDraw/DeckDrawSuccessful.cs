using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DeckDraw;

/// <summary>
/// Represents a successful deck draw action result in Kings and Lows player-vs-deck scenario.
/// </summary>
public class DeckDrawSuccessful
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public required Guid GameId { get; init; }

	/// <summary>
	/// The number of cards that were discarded from the deck hand.
	/// </summary>
	public required int CardsDiscarded { get; init; }

	/// <summary>
	/// The number of new cards that were drawn for the deck hand.
	/// </summary>
	public required int CardsDrawn { get; init; }

	/// <summary>
	/// The next phase of the game after the deck draw action.
	/// </summary>
	public required string NextPhase { get; init; }

	/// <summary>
	/// The cards that were discarded from the deck hand.
	/// </summary>
	public IReadOnlyCollection<DeckCardInfo>? DiscardedCards { get; init; }

	/// <summary>
	/// The new cards dealt to the deck hand to replace discarded cards.
	/// </summary>
	public IReadOnlyCollection<DeckCardInfo>? NewCards { get; init; }

	/// <summary>
	/// The complete deck hand after drawing (all 5 cards).
	/// </summary>
	public IReadOnlyCollection<DeckCardInfo>? FinalHand { get; init; }

	/// <summary>
	/// The hand description for the deck's final hand (e.g., "Full House, Kings over Sevens").
	/// </summary>
	public string? HandDescription { get; init; }
}

/// <summary>
/// Represents a card in the deck hand for the draw result.
/// </summary>
public record DeckCardInfo
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
