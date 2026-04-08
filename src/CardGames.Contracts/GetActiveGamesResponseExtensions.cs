using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Contracts;

public partial record GetActiveGamesResponse
{
	[JsonPropertyName("leagueId")]
	public Guid? LeagueId { get; init; }

	[JsonPropertyName("isDealersChoice")]
	public bool IsDealersChoice { get; init; }

	[JsonPropertyName("tournamentBuyIn")]
	public int? TournamentBuyIn { get; init; }

	[JsonIgnore]
	public bool IsLeagueGame => LeagueId.HasValue;

	[JsonIgnore]
	public string TableScopeLabel => IsLeagueGame ? "League" : "Public";

	[JsonIgnore]
	public bool IsTournament => TournamentBuyIn is > 0;

	[JsonIgnore]
	public string TableTypeLabel => IsTournament ? "Tournament" : "Cash";
}
