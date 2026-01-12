using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetTableSettings;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.UpdateTableSettings;

/// <summary>
/// Maps Game entity to UpdateTableSettingsResponse.
/// </summary>
public static class UpdateTableSettingsMapper
{
    /// <summary>
    /// Maps a Game entity to an UpdateTableSettingsResponse.
    /// </summary>
    /// <param name="game">The game entity with GameType loaded.</param>
    /// <param name="seatedPlayerCount">The number of players currently seated.</param>
    /// <returns>The mapped UpdateTableSettingsResponse.</returns>
    public static UpdateTableSettingsResponse MapToResponse(Game game, int seatedPlayerCount)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(game.GameType);

        return new UpdateTableSettingsResponse
        {
            GameId = game.Id,
            Name = game.Name,
            GameTypeCode = game.GameType.Code,
            GameTypeName = game.GameType.Name,
            CurrentPhase = game.CurrentPhase,
            IsEditable = GetTableSettingsMapper.IsPhaseEditable(game.CurrentPhase),
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
}
