using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.RedRiver;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Red River poker.
/// Uses Hold 'Em dealing and betting flow with a conditional bonus board card after river.
/// </summary>
public sealed class RedRiverFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => "REDRIVER";

    public override GameRules GetGameRules() => RedRiverRules.CreateGameRules();

    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.CommunityCard,
            InitialCardsPerPlayer = 2,
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
            context.Logger.LogError(ex, "Error performing auto-betting action for Red River game {GameId}", context.GameId);
        }
    }
}
