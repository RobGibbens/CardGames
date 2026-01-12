using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Poker.Betting;

/// <summary>
/// Defines the betting structure used by a poker game variant.
/// </summary>
public enum BettingStructure
{
	/// <summary>
	/// Uses ante bets only (e.g., Five Card Draw, Kings and Lows).
	/// </summary>
	Ante = 0,

	/// <summary>
	/// Uses small blind and big blind (e.g., Texas Hold'em, Omaha).
	/// </summary>
	Blinds = 1,

	/// <summary>
	/// Uses ante plus bring-in for lowest visible card (e.g., Seven Card Stud).
	/// </summary>
	AnteBringIn = 2,

	/// <summary>
	/// Uses ante only with drop-or-stay mechanics and pot matching (e.g., Kings and Lows).
	/// </summary>
	AntePotMatch = 3
}
