using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Contracts;

public partial record GetActiveGamesResponse
{
	[JsonPropertyName("isDealersChoice")]
	public bool IsDealersChoice { get; init; }
}
