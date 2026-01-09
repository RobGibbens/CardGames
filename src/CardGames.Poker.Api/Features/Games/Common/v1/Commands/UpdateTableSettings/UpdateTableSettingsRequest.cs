namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.UpdateTableSettings;

/// <summary>
/// Request model for updating table settings.
/// </summary>
/// <param name="Name">The display name of the table.</param>
/// <param name="Ante">The ante amount required from each player.</param>
/// <param name="MinBet">The minimum bet amount.</param>
/// <param name="SmallBlind">The small blind amount (for blind-based games).</param>
/// <param name="BigBlind">The big blind amount (for blind-based games).</param>
/// <param name="RowVersion">Concurrency token for optimistic locking.</param>
public record UpdateTableSettingsRequest(
    string? Name,
    int? Ante,
    int? MinBet,
    int? SmallBlind,
    int? BigBlind,
    string RowVersion);
