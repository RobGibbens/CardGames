using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Hands.StudHands;

/// <summary>
/// Pair Pressure hand: a seven card stud variant where face-up paired ranks become wild,
/// and only the two most recent paired ranks remain active.
/// </summary>
public class PairPressureHand : StudHand
{
	private readonly PairPressureWildCardRules _wildCardRules;
	private readonly IReadOnlyCollection<Card> _faceUpCardsInOrder;
	private IReadOnlyCollection<Card> _wildCards;
	private HandType _evaluatedType;
	private long _evaluatedStrength;
	private IReadOnlyCollection<Card> _evaluatedBestCards;
	private IReadOnlyCollection<Card> _bestHandSourceCards;
	private bool _evaluated;

	public IReadOnlyCollection<Card> WildCards => _wildCards ??= DetermineWildCards();

	public IReadOnlyCollection<Card> EvaluatedBestCards
	{
		get
		{
			EvaluateIfNeeded();
			return _evaluatedBestCards;
		}
	}

	public IReadOnlyCollection<Card> BestHandSourceCards
	{
		get
		{
			EvaluateIfNeeded();
			return _bestHandSourceCards;
		}
	}

	public PairPressureHand(
		IReadOnlyCollection<Card> holeCards,
		IReadOnlyCollection<Card> openCards,
		Card downCard,
		IReadOnlyCollection<Card> faceUpCardsInOrder)
		: base(holeCards, openCards, downCard != null ? [downCard] : Array.Empty<Card>())
	{
		if (holeCards.Count != 2)
		{
			throw new ArgumentException("Pair Pressure needs exactly two hole cards", nameof(holeCards));
		}

		if (openCards.Count > 4)
		{
			throw new ArgumentException("Pair Pressure has at most four open cards", nameof(openCards));
		}

		_wildCardRules = new PairPressureWildCardRules();
		_faceUpCardsInOrder = faceUpCardsInOrder;
	}

	private IReadOnlyCollection<Card> DetermineWildCards()
		=> _wildCardRules.DetermineWildCards(Cards, _faceUpCardsInOrder);

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
		if (_evaluated)
		{
			return;
		}

		_evaluated = true;

		if (!WildCards.Any())
		{
			_evaluatedType = base.DetermineType();
			_evaluatedStrength = base.CalculateStrength();
			_evaluatedBestCards = FindBestFiveCardHand();
			_bestHandSourceCards = _evaluatedBestCards;
			return;
		}

		var (type, strength, evaluatedCards, sourceCards) = WildCardHandEvaluator.EvaluateBestHand(
			Cards, WildCards, Ranking);
		_evaluatedType = type;
		_evaluatedStrength = strength;
		_evaluatedBestCards = evaluatedCards;
		_bestHandSourceCards = sourceCards;
	}

	private IReadOnlyCollection<Card> FindBestFiveCardHand()
	{
		return PossibleHands()
			.Select(hand => new { hand, type = HandTypeDetermination.DetermineHandType(hand) })
			.Where(pair => pair.type == Type)
			.OrderByDescending(pair => HandStrength.Calculate(pair.hand.ToList(), pair.type, Ranking))
			.First()
			.hand
			.ToList();
	}
}