using CardGames.Contracts.TableSettings;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Games.FiveCardDraw;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetTableSettings;

/// <summary>
/// Maps Game entity to table settings response types.
/// </summary>
public static class GetTableSettingsMapper
{
    /// <summary>
    /// Phases during which table settings can be edited.
    /// </summary>
    private static readonly HashSet<string> EditablePhases =
    [
        nameof(FiveCardDrawPhase.WaitingToStart),
        "WaitingForPlayers"
    ];

    /// <summary>
    /// Maps a Game entity to a GetTableSettingsResponse.
    /// </summary>
    /// <param name="game">The game entity with GameType loaded.</param>
    /// <param name="seatedPlayerCount">The number of players currently seated.</param>
    /// <returns>The mapped GetTableSettingsResponse.</returns>
    public static GetTableSettingsResponse MapToResponse(Game game, int seatedPlayerCount)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(game.GameType);

        return new GetTableSettingsResponse
        {
            GameId = game.Id,
            Name = game.Name,
            GameTypeCode = game.GameType.Code,
            GameTypeName = game.GameType.Name,
            CurrentPhase = game.CurrentPhase,
            IsEditable = IsPhaseEditable(game.CurrentPhase),
            Ante = game.Ante,
            MinBet = game.MinBet,
            SmallBlind = game.SmallBlind,
            BigBlind = game.BigBlind,
            MaxPlayers = game.GameType.MaxPlayers,
            MinPlayers = game.GameType.MinPlayers,
            SeatedPlayerCount = seatedPlayerCount,
            CreatedById = game.CreatedById,
            CreatedByName = game.CreatedByName,
            UpdatedAt = game.UpdatedAt,
            UpdatedById = game.UpdatedById,
            UpdatedByName = game.UpdatedByName,
            RowVersion = game.RowVersion.ToBase64String()
        };
    }

    /// <summary>
    /// Maps a Game entity to a TableSettingsDto (for SignalR broadcasts).
    /// </summary>
    /// <param name="game">The game entity with GameType loaded.</param>
    /// <param name="seatedPlayerCount">The number of players currently seated.</param>
    /// <returns>The mapped TableSettingsDto.</returns>
    public static TableSettingsDto MapToDto(Game game, int seatedPlayerCount)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(game.GameType);

        return new TableSettingsDto
        {
            GameId = game.Id,
            Name = game.Name,
            GameTypeCode = game.GameType.Code,
            GameTypeName = game.GameType.Name,
            CurrentPhase = game.CurrentPhase,
            IsEditable = IsPhaseEditable(game.CurrentPhase),
            Ante = game.Ante,
            MinBet = game.MinBet,
            SmallBlind = game.SmallBlind,
            BigBlind = game.BigBlind,
            MaxPlayers = game.GameType.MaxPlayers,
            MinPlayers = game.GameType.MinPlayers,
            SeatedPlayerCount = seatedPlayerCount,
            CreatedById = game.CreatedById,
            CreatedByName = game.CreatedByName,
            UpdatedAt = game.UpdatedAt,
            UpdatedById = game.UpdatedById,
            UpdatedByName = game.UpdatedByName,
            RowVersion = game.RowVersion.ToBase64String()
        };
        }

        /// <summary>
        /// Determines if the given phase allows editing of table settings.
        /// </summary>
        /// <param name="phase">The current phase name.</param>
        /// <returns>True if settings can be edited; otherwise false.</returns>
        public static bool IsPhaseEditable(string phase)
        {
            return EditablePhases.Contains(phase);
        }
    }
