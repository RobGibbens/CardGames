using System.ComponentModel;

namespace CardGames.Poker.Games.KingsAndLows;

/// <summary>
/// Represents the current phase of a Kings and Lows hand.
/// </summary>
public enum KingsAndLowsPhase
{
	/// <summary>Waiting for hand to start.</summary>
	[Description("Waiting to Start")]
	WaitingToStart,

	/// <summary>Collecting antes from all players.</summary>
	[Description("Collecting Antes")]
	CollectingAntes,

	/// <summary>Dealing initial 5 cards to all players.</summary>
	[Description("Dealing")]
	Dealing,

	/// <summary>Drop-or-stay decision phase where players simultaneously decide to drop or stay.</summary>
	[Description("Drop or Stay")] 
	DropOrStay,

	/// <summary>Draw phase where staying players discard and draw cards.</summary>
	[Description("Drawing")]
	DrawPhase,

	/// <summary>
	/// Draw complete - all players have drawn their cards.
	/// This is a brief display phase before showdown begins.
	/// </summary>
	[Description("Draw complete")]
	DrawComplete,

	/// <summary>
	/// Special case: Only one player stayed - they play against the deck.
	/// Dealer deals a dummy hand from remaining cards.
	/// </summary>
	[Description("Player vs Deck")] 
	PlayerVsDeck,

	/// <summary>Showdown - comparing hands to determine winner.</summary>
	[Description("Showdown")] 
	Showdown,

	/// <summary>
	/// Losers must match the pot. This phase handles pot matching.
	/// </summary>
	[Description("Pot Matching")] 
	PotMatching,

	/// <summary>Hand is complete.</summary>
	[Description("Complete")] 
	Complete
}
