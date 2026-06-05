using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;

namespace CardGames.Poker.Hands;

public abstract class HandBase : IComparable<HandBase>
{
	protected HandBase(IReadOnlyCollection<Card> cards)
	{
		Cards = cards;
	}

	public IReadOnlyCollection<Card> Cards { get; }

	public long Strength => field != 0
		? field
		: field = CalculateStrength();

	public HandType Type => field != HandType.Incomplete
		? field
		: field = DetermineType();


	protected abstract HandTypeStrengthRanking Ranking { get; }

	public int CompareTo(HandBase other)
	{
		return Strength.CompareTo(other.Strength);
	}

	protected abstract IEnumerable<IReadOnlyCollection<Card>> PossibleHands();

	protected virtual long CalculateStrength()
	{
		var handsAndTypes = PossibleHands()
			.Select(hand => new { hand, type = HandTypeDetermination.DetermineHandType(hand) });

		return handsAndTypes
			.Where(pair => pair.type == Type)
			.Select(pair => pair.hand)
			.Select(hand => HandStrength.Calculate(hand, Type, Ranking))
			.Max();
	}

	protected virtual HandType DetermineType()
	{
		var handsAndTypes = PossibleHands()
			.Select(hand => new { hand, type = HandTypeDetermination.DetermineHandType(hand) });

		return HandStrength.GetEffectiveType(handsAndTypes.Select(pair => pair.type), Ranking);
	}

	public static bool operator >(HandBase thisHand, HandBase otherHand)
	{
		return thisHand.CompareTo(otherHand) > 0;
	}

	public static bool operator <(HandBase thisHand, HandBase otherHand)
	{
		return thisHand.CompareTo(otherHand) < 0;
	}

	public override string ToString()
	{
		return Cards.ToStringRepresentation();
	}
}