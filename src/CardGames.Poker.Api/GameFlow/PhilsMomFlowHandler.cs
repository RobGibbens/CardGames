using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.FoldDuringDraw;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.PhilsMom;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Phil's Mom poker.
/// Deals 4 hole cards with blinds and uses two single-card discard rounds.
/// </summary>
public sealed class PhilsMomFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => "PHILSMOM";

    public override GameRules GetGameRules() => PhilsMomRules.CreateGameRules();

    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.CommunityCard,
            InitialCardsPerPlayer = 4,
            AllFaceDown = true
        };
    }

    public override bool SkipsAnteCollection => true;

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
        await CollectBlindsAsync(context, game, eligiblePlayers, now, cancellationToken);
        await base.DealDrawStyleCardsAsync(context, game, eligiblePlayers, now, cancellationToken);
    }

    /// <summary>
    /// Draw-phase timeout folds the actor because Phil's Mom requires a forced discard.
    /// </summary>
    public override async Task PerformAutoActionAsync(AutoActionContext context)
    {
        if (IsDrawingPhase(context.CurrentPhase) && context.PlayerSeatIndex >= 0)
        {
            context.Logger.LogInformation(
                "Phil's Mom auto-action: folding player at seat {SeatIndex} during draw phase for game {GameId}",
                context.PlayerSeatIndex, context.GameId);

            var command = new FoldDuringDrawCommand(context.GameId, context.PlayerSeatIndex);

            try
            {
                await context.Mediator.Send(command, context.CancellationToken);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, "Error performing auto-fold during draw phase for Phil's Mom game {GameId}", context.GameId);
            }

            return;
        }

        await base.PerformAutoActionAsync(context);
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
            context.Logger.LogError(ex, "Error performing auto-betting action for Phil's Mom game {GameId}", context.GameId);
        }
    }
}
