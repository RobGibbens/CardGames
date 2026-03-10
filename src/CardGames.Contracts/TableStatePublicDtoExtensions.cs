using System.Text.Json.Serialization;

namespace CardGames.Contracts.SignalR;

public sealed partial record TableStatePublicDto
{
    [JsonPropertyName("areOddsVisibleToAllPlayers")]
    public bool? AreOddsVisibleToAllPlayers { get; init; }
}
