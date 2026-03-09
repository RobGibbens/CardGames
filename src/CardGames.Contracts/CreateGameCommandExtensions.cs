using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Contracts;

public partial record CreateGameCommand
{
	[JsonPropertyName("isDealersChoice")]
	public bool IsDealersChoice { get; init; }

	[JsonPropertyName("smallBlind")]
	public int? SmallBlind { get; init; }

	[JsonPropertyName("bigBlind")]
	public int? BigBlind { get; init; }

	[JsonPropertyName("areOddsVisibleToAllPlayers")]
	public bool AreOddsVisibleToAllPlayers { get; init; } = true;
}
