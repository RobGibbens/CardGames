using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.HoldTheBaseball;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Hold the Baseball poker (Hold 'Em with 3s and 9s wild).
/// </summary>
public sealed class HoldTheBaseballFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => "HOLDTHEBASEBALL";

    public override GameRules GetGameRules() => HoldTheBaseballRules.CreateGameRules();

    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.CommunityCard,
            InitialCardsPerPlayer = 2,
            AllFaceDown = true
        };
    }

    public override bool SkipsAnteCollection => true; // Uses blinds instead

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

    protected override async Task SendBettingActionAsync(AutoActionContext context, BettingActionType action, int amount = 0)
    {
        var command = new Features.Games.HoldTheBaseball.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand(
            context.GameId, action, amount);
        try
        {
            await context.Mediator.Send(command, context.CancellationToken);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Error performing auto-betting action for Hold the Baseball game {GameId}", context.GameId);
        }
    }
}
