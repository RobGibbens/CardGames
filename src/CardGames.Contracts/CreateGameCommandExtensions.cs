using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Contracts;

public partial record CreateGameCommand
{
	[JsonPropertyName("isDealersChoice")]
	public bool IsDealersChoice { get; init; }
}
