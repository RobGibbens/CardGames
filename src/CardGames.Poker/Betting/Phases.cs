using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace CardGames.Poker.Betting;

public enum Phases
{
	/// <summary>Waiting for hand to start.</summary>
	[Description("Waiting to Start")]
	WaitingToStart,

	/// <summary>Collecting antes from all players.</summary>
	[Description("Collecting Antes")]
	CollectingAntes,

	/// <summary>Collecting blinds from players.</summary>
	[Description("Collecting Blinds")]
	CollectingBlinds,

	/// <summary>Dealing initial cards to players.</summary>
	[Description("Dealing Cards")]
	Dealing,

	/// <summary>Pre-flop betting round.</summary>
	[Description("Pre-Flop")]
	PreFlop,

	/// <summary>Flop betting round (after 3 community cards).</summary>
	[Description("Flop")]
	Flop,

	/// <summary>Drop-or-stay decision phase where players simultaneously decide to drop or stay.</summary>
	[Description("Drop or Stay")]
	DropOrStay,

	/// <summary>Turn betting round (after 4th community card).</summary>
	[Description("Turn")]
	Turn,

	/// <summary>River betting round (after 5th community card).</summary>
	[Description("River")]
	River,

	/// <summary>First betting round (pre-draw).</summary>
	[Description("First Betting Round")]
	FirstBettingRound,

	/// <summary>Draw phase where players can discard and draw cards.</summary>
	[Description("Draw Phase")]
	DrawPhase,

	/// <summary>
	/// Draw complete - all players have drawn their cards.
	/// This is a brief display phase before showdown begins.
	/// </summary>
	[Description("Draw complete")]
	DrawComplete,

	/// <summary>
	/// Losers must match the pot. This phase handles pot matching.
	/// </summary>
	[Description("Pot Matching")]
	PotMatching,

	/// <summary>
	/// Special case: Only one player stayed - they play against the deck.
	/// Dealer deals a dummy hand from remaining cards.
	/// </summary>
	[Description("Player vs Deck")]
	PlayerVsDeck,

	/// <summary>Second betting round (post-draw).</summary>
	[Description("Second Betting Round")]
	SecondBettingRound,

	/// <summary>Third street: 2 down cards and 1 up card dealt, bring-in betting round.</summary>
	[Description("Third Street")]
	ThirdStreet,

	/// <summary>Offering buy-card option to players who received a 4 face up.</summary>
	[Description("Buy Card Option")]
	BuyCardOffer,

	/// <summary>Fourth street: 1 up card dealt, betting round (small bet).</summary>
	[Description("Fourth Street")]
	FourthStreet,

	/// <summary>Fifth street: 1 up card dealt, betting round (big bet).</summary>
	[Description("Fifth Street")]
	FifthStreet,

	/// <summary>Sixth street: 1 up card dealt, betting round (big bet).</summary>
	[Description("Sixth Street")]
	SixthStreet,

	/// <summary>Seventh street (river): 1 down card dealt, final betting round (big bet).</summary>
	[Description("Seventh Street")]
	SeventhStreet,
	
	/// <summary>Showdown - comparing hands to determine winner.</summary>
	[Description("Showdown")]
	Showdown,

	/// <summary>Hand is complete.</summary>
	[Description("Complete")]
	Complete,

	/// <summary>Waiting for players to join.</summary>
	[Description("Waiting for Players")]
	WaitingForPlayers
}