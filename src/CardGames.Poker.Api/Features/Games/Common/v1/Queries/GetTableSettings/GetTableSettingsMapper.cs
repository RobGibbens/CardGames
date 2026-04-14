using CardGames.Contracts.TableSettings;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.FiveCardDraw;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetTableSettings;

/// <summary>
/// Maps Game entity to table settings response types.
/// </summary>
public static class GetTableSettingsMapper
{
    private const string DealersChoiceGameTypeCode = "DEALERSCHOICE";
    private const string DealersChoiceGameTypeName = "Dealer's Choice";
    private const int DealersChoiceMinPlayers = 2;
    private const int DealersChoiceMaxPlayers = 8;

    /// <summary>
    /// Phases during which table settings can be edited.
    /// </summary>
    private static readonly HashSet<string> EditablePhases =
    [
        nameof(Phases.WaitingToStart),
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
        var (gameTypeCode, gameTypeName, minPlayers, maxPlayers) = ResolveGameType(game);

        return new GetTableSettingsResponse
        {
            GameId = game.Id,
            Name = game.Name,
            GameTypeCode = gameTypeCode,
            GameTypeName = gameTypeName,
            CurrentPhase = game.CurrentPhase,
            IsEditable = IsPhaseEditable(game.CurrentPhase),
            Ante = game.Ante,
            MinBet = game.MinBet,
            SmallBlind = game.SmallBlind,
            BigBlind = game.BigBlind,
			MaxBuyIn = game.MaxBuyIn,
            AreOddsVisibleToAllPlayers = game.AreOddsVisibleToAllPlayers,
            MaxPlayers = maxPlayers,
            MinPlayers = minPlayers,
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
        var (gameTypeCode, gameTypeName, minPlayers, maxPlayers) = ResolveGameType(game);

        return new TableSettingsDto
        {
            GameId = game.Id,
            Name = game.Name,
            GameTypeCode = gameTypeCode,
            GameTypeName = gameTypeName,
            CurrentPhase = game.CurrentPhase,
            IsEditable = IsPhaseEditable(game.CurrentPhase),
            Ante = game.Ante,
            MinBet = game.MinBet,
            SmallBlind = game.SmallBlind,
            BigBlind = game.BigBlind,
			MaxBuyIn = game.MaxBuyIn,
            AreOddsVisibleToAllPlayers = game.AreOddsVisibleToAllPlayers,
            MaxPlayers = maxPlayers,
            MinPlayers = minPlayers,
            SeatedPlayerCount = seatedPlayerCount,
            CreatedById = game.CreatedById,
            CreatedByName = game.CreatedByName,
            UpdatedAt = game.UpdatedAt,
            UpdatedById = game.UpdatedById,
            UpdatedByName = game.UpdatedByName,
            RowVersion = game.RowVersion.ToBase64String()
        };
        }

        internal static (string GameTypeCode, string GameTypeName, int MinPlayers, int MaxPlayers) ResolveGameType(Game game)
        {
            ArgumentNullException.ThrowIfNull(game);

            if (game.GameType is not null)
            {
                return (game.GameType.Code, game.GameType.Name, game.GameType.MinPlayers, game.GameType.MaxPlayers);
            }

            if (game.IsDealersChoice)
            {
                return (DealersChoiceGameTypeCode, DealersChoiceGameTypeName, DealersChoiceMinPlayers, DealersChoiceMaxPlayers);
            }

            return (string.Empty, "Unknown Game", 0, 0);
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
