using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Poker;

/// <summary>
/// Defines the wild card rule type for a poker game variant.
/// </summary>
public enum WildCardRule
{
	/// <summary>
	/// No wild cards in this game.
	/// </summary>
	None = 0,

	/// <summary>
	/// Specific ranks are always wild (e.g., 3s and 9s in Baseball).
	/// </summary>
	FixedRanks = 1,

	/// <summary>
	/// Dynamic wild cards based on dealt cards (e.g., Follow the Queen).
	/// </summary>
	Dynamic = 2,

	/// <summary>
	/// Lowest card in hand is wild, plus specific ranks (e.g., Kings and Lows).
	/// </summary>
	LowestCard = 3
}
