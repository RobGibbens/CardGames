using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Contracts;

/// <summary>
/// Extension for DrawCardsSuccessful to add the NewHandDescription property
/// that describes the player's new hand after drawing cards.
/// </summary>
public partial record DrawCardsSuccessful
{
    /// <summary>
    /// A description of the player's new hand after the draw (e.g., "Pair of Kings", "Full house, Jacks full of Fives").
    /// </summary>
    [JsonPropertyName("newHandDescription")]
    public string? NewHandDescription { get; init; }
}
