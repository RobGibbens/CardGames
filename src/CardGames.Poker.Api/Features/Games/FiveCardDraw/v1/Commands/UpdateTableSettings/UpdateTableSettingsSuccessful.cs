namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.UpdateTableSettings;

/// <summary>
/// Successful result of updating table settings.
/// </summary>
public record UpdateTableSettingsSuccessful
{
    /// <summary>
    /// The unique identifier of the updated game.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The updated table settings.
    /// </summary>
    public required UpdateTableSettingsResponse Settings { get; init; }
}
