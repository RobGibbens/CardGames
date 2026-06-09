using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.Baseball;

/// <summary>
///     Represents a player in a Baseball game with their cards and betting state.
///     Baseball players may have more than 7 cards if they receive 4s face up.
/// </summary>
public class BaseballGamePlayer
{
	/// <summary>
	///     Tracks which board cards are 4s that offer a buy-card option.
	///     Key is the board card index, value is whether the player has been offered/declined.
	/// </summary>
	private readonly HashSet<int> _pendingFourOffers = [];

	public BaseballGamePlayer(PokerPlayer player)
	{
		Player = player;
	}

	public PokerPlayer Player { get; }
	public List<Card> HoleCards { get; } = new();
	public List<Card> BoardCards { get; } = new();

	public IEnumerable<Card> AllCards => HoleCards.Concat(BoardCards);

	/// <summary>
	///     Checks if this player has any pending 4 offers.
	/// </summary>
	public bool HasPendingFourOffer => _pendingFourOffers.Count > 0;

	public void AddHoleCard(Card card)
	{
		HoleCards.Add(card);
	}

	public void AddBoardCard(Card card)
	{
		BoardCards.Add(card);

		// Track if this is a 4 that could trigger a buy-card offer
		if (card.Symbol == Symbol.Four) _pendingFourOffers.Add(BoardCards.Count - 1);
	}

	/// <summary>
	///     Gets the indices of board cards that are 4s with pending buy offers.
	/// </summary>
	public IReadOnlyCollection<int> GetPendingFourOffers()
	{
		return _pendingFourOffers.ToList();
	}

	/// <summary>
	///     Marks a four's buy offer as handled (either bought or declined).
	/// </summary>
	public void ClearPendingFourOffer(int boardCardIndex)
	{
		_pendingFourOffers.Remove(boardCardIndex);
	}

	/// <summary>
	///     Clears all pending four offers (used when moving to next phase).
	/// </summary>
	public void ClearAllPendingFourOffers()
	{
		_pendingFourOffers.Clear();
	}

	public void ResetHand()
	{
		HoleCards.Clear();
		BoardCards.Clear();
		_pendingFourOffers.Clear();
	}
}