using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.IrishHoldEm;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Irish Hold 'Em poker.
/// Deals 4 hole cards like Omaha; after the flop betting round, players discard 2,
/// then play continues as Texas Hold 'Em with community cards.
/// </summary>
public sealed class IrishHoldEmFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => "IRISHHOLDEM";

    public override GameRules GetGameRules() => IrishHoldEmRules.CreateGameRules();

    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.CommunityCard,
            InitialCardsPerPlayer = 4,
            AllFaceDown = true
        };
    }

    public override bool SkipsAnteCollection => true; // Uses blinds

    public override string GetInitialPhase(Game game)
    {
        return "CollectingBlinds";
    }

    public override async Task DealCardsAsync(
        CardsDbContext context,
        Game game,
        List<GamePlayer> eligiblePlayers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // 1. Collect Blinds
        await CollectBlindsAsync(context, game, eligiblePlayers, now, cancellationToken);

        // 2. Deal Hole Cards
        await base.DealDrawStyleCardsAsync(context, game, eligiblePlayers, now, cancellationToken);
    }

    /// <summary>
    /// After Irish Hold 'Em discard phase completes, transition to Turn (not SecondBettingRound).
    /// The Turn community card is dealt followed by the Turn betting round.
    /// </summary>
    public override Task<string> ProcessDrawCompleteAsync(
        CardsDbContext context,
        Game game,
        IHandHistoryRecorder handHistoryRecorder,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(nameof(Phases.Turn));
    }

    protected override async Task SendBettingActionAsync(AutoActionContext context, Data.Entities.BettingActionType action, int amount = 0)
    {
        var command = new Features.Games.HoldEm.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand(
            context.GameId, action, amount);

        try
        {
            await context.Mediator.Send(command, context.CancellationToken);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Error performing auto-betting action for Irish Hold 'Em game {GameId}", context.GameId);
        }
    }
}
