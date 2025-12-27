using CardGames.Contracts.TableSettings;

namespace CardGames.Contracts.SignalR;

/// <summary>
/// DTO broadcast to game clients when table settings are updated.
/// </summary>
public sealed record TableSettingsUpdatedDto
{
    /// <summary>
    /// The unique identifier of the game/table.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The unique identifier of the user who made the update.
    /// </summary>
    public string? UpdatedById { get; init; }

    /// <summary>
    /// The name of the user who made the update.
    /// </summary>
    public string? UpdatedByName { get; init; }

    /// <summary>
    /// When the update was made.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// The updated table settings.
    /// </summary>
    public required TableSettingsDto Settings { get; init; }
}
