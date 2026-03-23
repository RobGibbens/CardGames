namespace CardGames.Poker.Games.PairPressure;

/// <summary>
/// Represents the current phase of a Pair Pressure hand.
/// </summary>
public enum PairPressurePhase
{
	WaitingToStart,
	CollectingAntes,
	ThirdStreet,
	FourthStreet,
	FifthStreet,
	SixthStreet,
	SeventhStreet,
	Showdown,
	Complete
}