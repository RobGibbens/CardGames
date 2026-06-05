namespace CardGames.Poker.Api.Contracts;

public partial record UpdateTableSettingsRequest
{
	[JsonPropertyName("areOddsVisibleToAllPlayers")]
	public bool? AreOddsVisibleToAllPlayers { get; init; }
}