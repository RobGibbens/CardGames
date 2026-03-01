using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Contracts;

/// <summary>
/// Extension properties for ShowdownPlayerHand that are not yet in the generated contract.
/// These will be available in the next contract regeneration from the OpenAPI spec.
/// </summary>
public partial record ShowdownPlayerHand
{
    /// <summary>
    /// The zero-based indices of cards in <see cref="Cards"/> that make up the best 5-card hand.
    /// For Seven Card Stud, players have 7 cards but only 5 count toward the winning hand.
    /// </summary>
    [JsonPropertyName("bestCardIndexes")]
    public ICollection<int>? BestCardIndexes { get; init; }

    /// <summary>
    /// Whether this player was eliminated by holding a card matching the Ugly card rank
    /// in The Good, the Bad, and the Ugly.
    /// </summary>
    [JsonPropertyName("isEliminatedByUgly")]
    public bool? IsEliminatedByUgly { get; init; }
}
