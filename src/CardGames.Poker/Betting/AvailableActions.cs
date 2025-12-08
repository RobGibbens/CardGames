using System.Collections.Generic;

namespace CardGames.Poker.Betting;

/// <summary>
/// Represents the available actions for a player at any point in a betting round.
/// </summary>
public class AvailableActions
{
	public bool CanCheck { get; init; }
	public bool CanBet { get; init; }
	public bool CanCall { get; init; }
	public bool CanRaise { get; init; }
	public bool CanFold { get; init; }
	public bool CanAllIn { get; init; }
	public int MinBet { get; init; }
	public int MaxBet { get; init; }
	public int CallAmount { get; init; }
	public int MinRaise { get; init; }

	public override string ToString()
	{
		var actions = new List<string>();
		if (CanCheck) actions.Add("check");
		if (CanBet) actions.Add($"bet ({MinBet}-{MaxBet})");
		if (CanCall) actions.Add($"call {CallAmount}");
		if (CanRaise) actions.Add($"raise (min {MinRaise})");
		if (CanFold) actions.Add("fold");
		if (CanAllIn) actions.Add($"all-in ({MaxBet})");
		return string.Join(", ", actions);
	}
}