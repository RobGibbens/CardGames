using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Contracts;

/// <summary>
/// Extension to add PlayerResults to the auto-generated HandHistoryEntryDto.
/// </summary>
public partial record HandHistoryEntryDto
{
    /// <summary>
    /// All players' results for this hand.
    /// Includes every player who was dealt into the hand.
    /// </summary>
    [JsonPropertyName("playerResults")]
    public ICollection<HandHistoryPlayerResultDto>? PlayerResults { get; init; }
}
