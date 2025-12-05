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

/// <summary>
/// Result of drawing cards (discard and draw) for a player.
/// </summary>
public class DrawCardsApiResult
{
	public bool Success { get; init; }
	public string? ErrorMessage { get; init; }
	public Guid PlayerId { get; init; }
	public string PlayerName { get; init; } = "";
	public int CardsDiscarded { get; init; }
	public List<string> NewCards { get; init; } = [];
	public List<string> NewHand { get; init; } = [];
	public bool DrawPhaseComplete { get; init; }
	public Guid? NextPlayerToAct { get; init; }
}

/// <summary>
/// Result of a showdown.
/// </summary>
public class ShowdownApiResult
{
	public bool Success { get; init; }
	public string? ErrorMessage { get; init; }
	public bool WonByFold { get; init; }
	public List<ShowdownPlayerResult> Results { get; init; } = [];
}

/// <summary>
/// Result for a single player in the showdown.
/// </summary>
public record ShowdownPlayerResult
{
	public Guid PlayerId { get; init; }
	public string PlayerName { get; init; } = "";
	public List<string> Hand { get; init; } = [];
	public string HandType { get; init; } = "";
	public string HandDescription { get; init; } = "";
	public int Payout { get; init; }
	public bool IsWinner { get; init; }
}
