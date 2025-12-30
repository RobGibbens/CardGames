using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.DrawHands;

namespace CardGames.Poker.Games.TwosJacksManWithTheAxe;

/// <summary>
/// Result of a showdown.
/// </summary>
public class ShowdownResult
{
	/// <summary>
	/// Gets a value indicating whether the showdown completed successfully.
	/// When <c>false</c>, check <see cref="ErrorMessage"/> for failure details.
	/// </summary>
	public bool Success { get; init; }

	/// <summary>
	/// Gets the error message when <see cref="Success"/> is <c>false</c>.
	/// Contains details about why the showdown failed, such as being called
	/// outside the showdown phase.
	/// </summary>
	public string ErrorMessage { get; init; }

	/// <summary>
	/// Gets the chip payouts awarded to each winning player.
	/// Keys are player names; values are the chip amounts won (total of sevens pool + hand pool).
	/// Multiple entries indicate a split pot scenario.
	/// </summary>
	public Dictionary<string, int> Payouts { get; init; }

	/// <summary>
	/// Gets the evaluated hands and cards for all players who reached showdown.
	/// Keys are player names; values contain the evaluated <see cref="TwosJacksManWithTheAxeDrawHand"/> (or <c>null</c> if won by fold)
	/// and the actual cards held. Use this to display hand rankings and winning combinations.
	/// </summary>
	public Dictionary<string, (TwosJacksManWithTheAxeDrawHand hand, IReadOnlyCollection<Card> cards)> PlayerHands { get; init; }

	/// <summary>
	/// Gets a value indicating whether the hand was won by all opponents folding.
	/// When <c>true</c>, the winner collected the pot without showing cards,
	/// and <see cref="PlayerHands"/> entries will have <c>null</c> for the hand evaluation.
	/// </summary>
	public bool WonByFold { get; init; }

	/// <summary>
	/// Gets the player names who won the sevens pool (had a natural pair of 7s).
	/// Empty if no players qualified for the sevens pool.
	/// </summary>
	public List<string> SevensWinners { get; init; } = [];

	/// <summary>
	/// Gets the player names who won the high hand pool.
	/// </summary>
	public List<string> HighHandWinners { get; init; } = [];

	/// <summary>
	/// Gets the amount won from the sevens pool per player.
	/// Keys are player names; values are chip amounts won from the sevens pool.
	/// </summary>
	public Dictionary<string, int> SevensPayouts { get; init; } = [];

	/// <summary>
	/// Gets the amount won from the high hand pool per player.
	/// Keys are player names; values are chip amounts won from the high hand pool.
	/// </summary>
	public Dictionary<string, int> HighHandPayouts { get; init; } = [];

	/// <summary>
	/// Gets whether the sevens pool was rolled into the high hand pool
	/// because no players had a natural pair of 7s.
	/// </summary>
	public bool SevensPoolRolledOver { get; init; }

	/// <summary>
	/// Gets the wild cards held by each player at showdown.
	/// Keys are player names; values are the wild cards in their hand.
	/// </summary>
	public Dictionary<string, IReadOnlyCollection<Card>> PlayerWildCards { get; init; } = [];
}