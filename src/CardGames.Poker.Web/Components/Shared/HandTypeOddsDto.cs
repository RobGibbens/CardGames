namespace CardGames.Poker.Web.Components.Shared;

/// <summary>
/// DTO for hand type odds display.
/// Maps to HandTypeOdds from the API response.
/// </summary>
public record HandTypeOddsDto(
    string HandType,
    string DisplayName,
    decimal Probability
);
