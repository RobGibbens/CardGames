using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.FoldDuringDraw;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.CrazyPineapple;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Crazy Pineapple poker.
/// Deals 3 hole cards with blinds and enforces a mandatory one-card discard after the flop.
/// </summary>
public sealed class CrazyPineappleFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => "CRAZYPINEAPPLE";

    public override GameRules GetGameRules() => CrazyPineappleRules.CreateGameRules();

    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.CommunityCard,
            InitialCardsPerPlayer = 3,
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
    /// Draw-phase timeout folds the actor because Crazy Pineapple requires a forced discard after the flop.
    /// </summary>
    public override async Task PerformAutoActionAsync(AutoActionContext context)
    {
        if (IsDrawingPhase(context.CurrentPhase) && context.PlayerSeatIndex >= 0)
        {
            context.Logger.LogInformation(
                "Crazy Pineapple auto-action: folding player at seat {SeatIndex} during draw phase for game {GameId}",
                context.PlayerSeatIndex, context.GameId);

            var command = new FoldDuringDrawCommand(context.GameId, context.PlayerSeatIndex);

            try
            {
                await context.Mediator.Send(command, context.CancellationToken);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, "Error performing auto-fold during draw phase for Crazy Pineapple game {GameId}", context.GameId);
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
            context.Logger.LogError(ex, "Error performing auto-betting action for Crazy Pineapple game {GameId}", context.GameId);
        }
    }
}
