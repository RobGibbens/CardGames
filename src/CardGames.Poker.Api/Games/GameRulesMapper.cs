using CardGames.Contracts.GameRules;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.Games;

/// <summary>
/// Maps game rules domain objects to DTOs.
/// </summary>
public static class GameRulesMapper
{
    /// <summary>
    /// Converts a GameRules domain object to a DTO.
    /// </summary>
    public static GameRulesDto ToDto(GameRules rules)
    {
        return new GameRulesDto
        {
            GameTypeCode = rules.GameTypeCode,
            GameTypeName = rules.GameTypeName,
            Description = rules.Description,
            MinPlayers = rules.MinPlayers,
            MaxPlayers = rules.MaxPlayers,
            Phases = rules.Phases.Select(ToDto).ToList(),
            CardDealing = ToDto(rules.CardDealing),
            Betting = ToDto(rules.Betting),
            Drawing = rules.Drawing != null ? ToDto(rules.Drawing) : null,
            Showdown = ToDto(rules.Showdown),
            SpecialRules = rules.SpecialRules
        };
    }

    private static PhaseDescriptorDto ToDto(GamePhaseDescriptor phase)
    {
        return new PhaseDescriptorDto
        {
            PhaseId = phase.PhaseId,
            Name = phase.Name,
            Description = phase.Description,
            Category = phase.Category,
            RequiresPlayerAction = phase.RequiresPlayerAction,
            AvailableActions = phase.AvailableActions,
            IsTerminal = phase.IsTerminal
        };
    }

    private static CardDealingConfigDto ToDto(CardDealingConfig config)
    {
        return new CardDealingConfigDto
        {
            InitialCards = config.InitialCards,
            InitialVisibility = config.InitialVisibility.ToString(),
            HasCommunityCards = config.HasCommunityCards,
            DealingRounds = config.DealingRounds?.Select(ToDto).ToList()
        };
    }

    private static DealingRoundDto ToDto(DealingRound round)
    {
        return new DealingRoundDto
        {
            CardCount = round.CardCount,
            Visibility = round.Visibility.ToString(),
            Target = round.Target.ToString()
        };
    }

    private static BettingConfigDto ToDto(BettingConfig config)
    {
        return new BettingConfigDto
        {
            HasAntes = config.HasAntes,
            HasBlinds = config.HasBlinds,
            BettingRounds = config.BettingRounds,
            Structure = config.Structure
        };
    }

    private static DrawingConfigDto ToDto(DrawingConfig config)
    {
        return new DrawingConfigDto
        {
            AllowsDrawing = config.AllowsDrawing,
            MaxDiscards = config.MaxDiscards,
            SpecialRules = config.SpecialRules,
            DrawingRounds = config.DrawingRounds
        };
    }

    private static ShowdownConfigDto ToDto(ShowdownConfig config)
    {
        return new ShowdownConfigDto
        {
            HandRanking = config.HandRanking,
            IsHighLow = config.IsHighLow,
            HasSpecialSplitRules = config.HasSpecialSplitRules,
            SpecialSplitDescription = config.SpecialSplitDescription
        };
    }
}
