namespace CardGames.Poker.Api.Features.Games.Domain.Enums;

/// <summary>
/// Phases within a single hand.
/// </summary>
public enum HandPhase
{
	/// <summary>No active hand</summary>
	None,

	/// <summary>Collecting forced bets (antes or blinds)</summary>
	CollectingAntes,

	/// <summary>Dealing initial cards</summary>
	Dealing,

	/// <summary>First round of betting</summary>
	FirstBettingRound,

	/// <summary>Draw phase (5-Card Draw specific)</summary>
	DrawPhase,

	/// <summary>Second round of betting</summary>
	SecondBettingRound,

	/// <summary>Flop betting (Hold'em/Omaha)</summary>
	FlopBetting,

	/// <summary>Turn betting (Hold'em/Omaha)</summary>
	TurnBetting,

	/// <summary>River betting (Hold'em/Omaha)</summary>
	RiverBetting,

	/// <summary>Final showdown</summary>
	Showdown,

	/// <summary>Hand complete, results distributed</summary>
	Complete
}