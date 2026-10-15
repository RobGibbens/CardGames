namespace CardGames.Poker.Api.Contracts;

public partial record GetActiveGamesResponse
{
	[JsonPropertyName("leagueId")] public Guid? LeagueId { get; init; }

	[JsonPropertyName("isDealersChoice")] public bool IsDealersChoice { get; init; }

	[JsonPropertyName("tournamentBuyIn")] public int? TournamentBuyIn { get; init; }

	[JsonIgnore] public bool IsLeagueGame => LeagueId.HasValue;

	[JsonIgnore] public string TableScopeLabel => IsLeagueGame ? "League" : "Public";

	[JsonIgnore] public bool IsTournament => TournamentBuyIn is > 0;

	[JsonIgnore] public string TableTypeLabel => IsTournament ? "Tournament" : "Cash";

	public const string DealersChoiceImageName = "dealerschoice.png";

	public const string DealersChoiceDisplayName = "Dealer's Choice";

	/// <summary>
	///     Image file for the lobby card. Dealer's Choice tables have no variant image until a hand is chosen.
	/// </summary>
	[JsonIgnore]
	public string? LobbyImageName =>
		string.IsNullOrWhiteSpace(GameTypeImageName)
			? IsDealersChoice ? DealersChoiceImageName : null
			: GameTypeImageName;

	/// <summary>
	///     Lobby label. A chosen Dealer's Choice variant is shown in parentheses; a table still waiting
	///     for a choice is labeled Dealer's Choice without an empty pair of parentheses.
	/// </summary>
	[JsonIgnore]
	public string LobbyGameTypeName
	{
		get
		{
			var name = FirstPresent(GameTypeMetadataName, GameTypeName);
			if (!IsDealersChoice)
				return name;

			if (string.IsNullOrWhiteSpace(name) ||
			    string.Equals(name, DealersChoiceDisplayName, StringComparison.OrdinalIgnoreCase))
				return DealersChoiceDisplayName;

			return $"{DealersChoiceDisplayName} ({name})";
		}
	}

	private static string FirstPresent(params string?[] values)
	{
		foreach (var value in values)
			if (!string.IsNullOrWhiteSpace(value))
				return value;

		return string.Empty;
	}
}