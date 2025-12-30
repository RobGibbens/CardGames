using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;
using CardGames.Poker.Games.GameFlow;
using ResponseCardDealingConfig = CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules.CardDealingConfig;
using ResponseDealingRound = CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules.DealingRound;
using ResponseBettingConfig = CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules.BettingConfig;
using ResponseDrawingConfig = CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules.DrawingConfig;
using ResponseShowdownConfig = CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules.ShowdownConfig;

namespace CardGames.Poker.Api.Games;

/// <summary>
/// Maps game rules domain objects to response objects.
/// </summary>
public static class GameRulesMapper
{
    /// <summary>
    /// Converts a GameRules domain object to a GetGameRulesResponse.
    /// </summary>
    public static GetGameRulesResponse ToResponse(GameRules rules)
    {
        return new GetGameRulesResponse
        {
            GameTypeCode = rules.GameTypeCode,
            GameTypeName = rules.GameTypeName,
            Description = rules.Description,
            MinPlayers = rules.MinPlayers,
            MaxPlayers = rules.MaxPlayers,
            Phases = rules.Phases.Select(ToPhaseDescriptor).ToList(),
            CardDealing = ToCardDealingConfig(rules.CardDealing),
            Betting = ToBettingConfig(rules.Betting),
            Drawing = rules.Drawing != null ? ToDrawingConfig(rules.Drawing) : null,
            Showdown = ToShowdownConfig(rules.Showdown),
            SpecialRules = rules.SpecialRules
        };
    }

    private static PhaseDescriptor ToPhaseDescriptor(GamePhaseDescriptor phase)
    {
        return new PhaseDescriptor
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

    private static ResponseCardDealingConfig ToCardDealingConfig(CardGames.Poker.Games.GameFlow.CardDealingConfig config)
    {
        return new ResponseCardDealingConfig
        {
            InitialCards = config.InitialCards,
            InitialVisibility = config.InitialVisibility.ToString(),
            HasCommunityCards = config.HasCommunityCards,
            DealingRounds = config.DealingRounds?.Select(ToDealingRound).ToList()
        };
    }

    private static ResponseDealingRound ToDealingRound(CardGames.Poker.Games.GameFlow.DealingRound round)
    {
        return new ResponseDealingRound
        {
            CardCount = round.CardCount,
            Visibility = round.Visibility.ToString(),
            Target = round.Target.ToString()
        };
    }

    private static ResponseBettingConfig ToBettingConfig(CardGames.Poker.Games.GameFlow.BettingConfig config)
    {
        return new ResponseBettingConfig
        {
            HasAntes = config.HasAntes,
            HasBlinds = config.HasBlinds,
            BettingRounds = config.BettingRounds,
            Structure = config.Structure
        };
    }

    private static ResponseDrawingConfig ToDrawingConfig(CardGames.Poker.Games.GameFlow.DrawingConfig config)
    {
        return new ResponseDrawingConfig
        {
            AllowsDrawing = config.AllowsDrawing,
            MaxDiscards = config.MaxDiscards,
            SpecialRules = config.SpecialRules,
            DrawingRounds = config.DrawingRounds
        };
    }

    private static ResponseShowdownConfig ToShowdownConfig(CardGames.Poker.Games.GameFlow.ShowdownConfig config)
    {
        return new ResponseShowdownConfig
        {
            HandRanking = config.HandRanking,
            IsHighLow = config.IsHighLow,
            HasSpecialSplitRules = config.HasSpecialSplitRules,
            SpecialSplitDescription = config.SpecialSplitDescription
        };
    }
}
