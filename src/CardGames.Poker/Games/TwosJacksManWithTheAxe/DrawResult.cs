using System.Collections.Generic;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.Games.TwosJacksManWithTheAxe;

/// <summary>
/// Result of a draw action.
/// </summary>
public class DrawResult
{
	/// <summary>
	/// Gets a value indicating whether the draw action completed successfully.
	/// When <c>false</c>, check <see cref="ErrorMessage"/> for failure details.
	/// </summary>
	public bool Success { get; init; }

	/// <summary>
	/// Gets the error message when <see cref="Success"/> is <c>false</c>.
	/// Contains details about why the draw action failed, such as invalid card indices
	/// or attempting to draw outside the draw phase.
	/// </summary>
	public string ErrorMessage { get; init; }

	/// <summary>
	/// Gets the name of the player who performed the draw action.
	/// Useful for logging and displaying draw results to users.
	/// </summary>
	public string PlayerName { get; init; }

	/// <summary>
	/// Gets the cards that were discarded from the player's hand.
	/// This collection may be empty if the player chose to stand pat (keep all cards).
	/// </summary>
	public IReadOnlyCollection<Card> DiscardedCards { get; init; }

	/// <summary>
	/// Gets the new cards dealt to the player to replace discarded cards.
	/// The count matches <see cref="DiscardedCards"/> count, maintaining a five-card hand.
	/// </summary>
	public IReadOnlyCollection<Card> NewCards { get; init; }

	/// <summary>
	/// Gets a value indicating whether all players have completed their draws.
	/// When <c>true</c>, the game has advanced to the second betting round.
	/// </summary>
	public bool DrawComplete { get; init; }
}