namespace CardGames.Poker.Api.Features.Games.Domain;

/// <summary>
/// Result of processing a betting action in the aggregate.
/// </summary>
public class BettingResult
{
	public bool Success { get; init; }
	public string? ErrorMessage { get; init; }
	public string ActionDescription { get; init; } = "";
	public int ActualAmount { get; init; }
	public bool RoundComplete { get; init; }
	public bool PhaseAdvanced { get; init; }
	public string? NewPhase { get; init; }
	public int PlayerChipStack { get; init; }
}

/// <summary>
/// Result of collecting antes from all players.
/// </summary>
public class CollectAntesResult
{
	public bool Success { get; init; }
	public string? ErrorMessage { get; init; }
	public Dictionary<Guid, int> PlayerAntes { get; init; } = [];
	public int TotalCollected { get; init; }
}

/// <summary>
/// Result of dealing cards to players.
/// </summary>
public class DealCardsResult
{
	public bool Success { get; init; }
	public string? ErrorMessage { get; init; }
	public Dictionary<Guid, int> PlayerCardCounts { get; init; } = [];
	public Dictionary<Guid, List<string>> PlayerCards { get; init; } = [];
}
