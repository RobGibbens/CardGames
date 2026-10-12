using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Hands.CommunityCardHands;

/// <summary>
///     Klondike Hold'em hand: a Hold'em-style hand with one wild community card
///     (the Klondike Card).
///     <b>Before reveal</b>: the Klondike Card is unknown — one phantom wild card
///     is injected so the evaluation treats it as the best possible card.
///     <b>After reveal</b>: that one card is wild and may be any rank and suit.
/// </summary>
public sealed class KlondikeHand : CommunityCardsHand
{
	private readonly bool _isRevealed;
	private readonly Card? _klondikeCard;
	private IReadOnlyCollection<Card> _bestHandSourceCards;
	private bool _evaluated;
	private IReadOnlyCollection<Card> _evaluatedBestCards;
	private long _evaluatedStrength;
	private HandType _evaluatedType;

	/// <summary>
	///     Post-reveal constructor: the Klondike Card is known. That one card is wild
	///     and may be any rank and suit.
	/// </summary>
	public KlondikeHand(
		IReadOnlyCollection<Card> holeCards,
		IReadOnlyCollection<Card> communityCards,
		Card klondikeCard)
		: base(0, 2, holeCards, communityCards, HandTypeStrengthRanking.Classic)
	{
		_klondikeCard = klondikeCard;
		_isRevealed = true;
	}

	/// <summary>
	///     Pre-reveal constructor: the Klondike Card is unknown. A phantom wild card is
	///     added to the evaluation so the hand assumes the best possible unknown card.
	/// </summary>
	public KlondikeHand(
		IReadOnlyCollection<Card> holeCards,
		IReadOnlyCollection<Card> communityCards)
		: base(0, 2, holeCards, communityCards, HandTypeStrengthRanking.Classic)
	{
		_klondikeCard = null;
		_isRevealed = false;
	}

	/// <summary>
	///     Gets the evaluated best 5-card hand after applying wild card logic.
	/// </summary>
	public IReadOnlyCollection<Card> EvaluatedBestCards
	{
		get
		{
			EvaluateIfNeeded();
			return _evaluatedBestCards;
		}
	}

	/// <summary>
	///     Gets the original cards from the player's hand that formed the best evaluated hand.
	/// </summary>
	public IReadOnlyCollection<Card> BestHandSourceCards
	{
		get
		{
			EvaluateIfNeeded();
			return _bestHandSourceCards;
		}
	}

	protected override long CalculateStrength()
	{
		EvaluateIfNeeded();
		return _evaluatedStrength;
	}

	protected override HandType DetermineType()
	{
		EvaluateIfNeeded();
		return _evaluatedType;
	}

	private void EvaluateIfNeeded()
	{
		if (_evaluated) return;

		_evaluated = true;

		var allCards = HoleCards.Concat(CommunityCards).ToList();
		List<Card> wildCards;

		if (_isRevealed && _klondikeCard is not null)
		{
			// After reveal: only the Klondike card itself is wild.
			wildCards = allCards.Where(c => c.Suit == _klondikeCard.Suit && c.Symbol == _klondikeCard.Symbol).ToList();
		}
		else
		{
			// Before reveal: inject a phantom wild card (unique sentinel that won't
			// collide with any real card). The evaluator will treat it as wild.
			// Use the (Suit, Symbol) constructor to avoid ToSymbol() validation.
			var phantom = new Card(Suit.Spades, (Symbol)1);
			allCards.Add(phantom);
			wildCards = [phantom];
		}

		var (type, strength, evaluatedCards, sourceCards) = WildCardHandEvaluator.EvaluateBestHand(
			allCards, wildCards, Ranking);

		_evaluatedType = type;
		_evaluatedStrength = strength;
		_evaluatedBestCards = evaluatedCards;
		_bestHandSourceCards = sourceCards;
	}
}