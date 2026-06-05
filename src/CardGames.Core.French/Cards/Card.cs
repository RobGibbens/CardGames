using System;
using CardGames.Core.French.Cards.Extensions;

namespace CardGames.Core.French.Cards;

public sealed class Card : IEquatable<Card>
{
	public Card(Suit suit, int value)
	{
		Suit = suit;
		Symbol = value.ToSymbol();
	}

	public Card(Suit suit, Symbol symbol)
	{
		Suit = suit;
		Symbol = symbol;
	}

	public Suit Suit { get; }

	public int Value
		=> (int)Symbol;

	public Symbol Symbol { get; }

	public bool Equals(Card other)
	{
		return other is not null
		       && Suit == other.Suit
		       && Symbol == other.Symbol;
	}

	public override string ToString()
	{
		return this.ToShortString();
	}

	public override bool Equals(object obj)
	{
		return obj is Card other && Equals(other);
	}

	public static bool operator ==(Card left, Card right)
	{
		return ReferenceEquals(left, right) || (left is not null && left.Equals(right));
	}

	public static bool operator !=(Card left, Card right)
	{
		return !(left == right);
	}

	// This is the standard mapping of suits and values to 1..52, which is also used in the SerilaizationExtensions.
	public override int GetHashCode()
	{
		return (int)Suit * 13 + Value - 2;
	}
}