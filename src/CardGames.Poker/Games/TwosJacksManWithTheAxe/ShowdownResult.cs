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
	/// Keys are player names; values are the chip amounts won.
	/// Multiple entries indicate a split pot scenario.
	/// </summary>
	public Dictionary<string, int> Payouts { get; init; }

	/// <summary>
	/// Gets the evaluated hands and cards for all players who reached showdown.
	/// Keys are player names; values contain the evaluated <see cref="DrawHand"/> (or <c>null</c> if won by fold)
	/// and the actual cards held. Use this to display hand rankings and winning combinations.
	/// </summary>
	public Dictionary<string, (DrawHand hand, IReadOnlyCollection<Card> cards)> PlayerHands { get; init; }

	/// <summary>
	/// Gets a value indicating whether the hand was won by all opponents folding.
	/// When <c>true</c>, the winner collected the pot without showing cards,
	/// and <see cref="PlayerHands"/> entries will have <c>null</c> for the hand evaluation.
	/// </summary>
	public bool WonByFold { get; init; }
}