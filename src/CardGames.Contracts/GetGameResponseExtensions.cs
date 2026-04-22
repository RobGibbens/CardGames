using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Contracts;

public partial record GetGameResponse
{
	[JsonPropertyName("leagueId")]
	public Guid? LeagueId { get; init; }

	[JsonPropertyName("isDealersChoice")]
	public bool IsDealersChoice { get; init; }

	[JsonPropertyName("dealersChoiceDealerPosition")]
	public int? DealersChoiceDealerPosition { get; init; }

	[JsonPropertyName("maxBuyIn")]
	public int? MaxBuyIn { get; init; }

	[JsonPropertyName("tournamentBuyIn")]
	public int? TournamentBuyIn { get; init; }

	[JsonPropertyName("areOddsVisibleToAllPlayers")]
	public bool AreOddsVisibleToAllPlayers { get; init; }

	[JsonPropertyName("requiresJoinApproval")]
	public bool RequiresJoinApproval { get; init; }
}
