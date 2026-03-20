using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.BobBarker;
using CardGames.Poker.Games.GameFlow;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Bob Barker.
/// Deals five hole cards to each player, a hidden dealer card to the table, then enters the showcase-selection phase.
/// </summary>
public sealed class BobBarkerFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => PokerGameMetadataRegistry.BobBarkerCode;

    public override GameRules GetGameRules() => BobBarkerRules.CreateGameRules();

    public override IReadOnlyList<string> SpecialPhases => [nameof(Phases.DrawPhase)];

    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.CommunityCard,
            InitialCardsPerPlayer = 5,
            AllFaceDown = true
        };
    }

    public override bool SkipsAnteCollection => true;

    public override string GetInitialPhase(Game game)
    {
        return nameof(Phases.CollectingBlinds);
    }

    public override async Task DealCardsAsync(
        CardsDbContext context,
        Game game,
        List<GamePlayer> eligiblePlayers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await CollectBlindsAsync(context, game, eligiblePlayers, now, cancellationToken);
        await DealDrawStyleCardsAsync(context, game, eligiblePlayers, now, cancellationToken);

        var hiddenDealerCard = await context.GameCards
            .Where(gc => gc.GameId == game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Deck)
            .OrderBy(gc => gc.DealOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (hiddenDealerCard is not null)
        {
            hiddenDealerCard.Location = CardLocation.Community;
            hiddenDealerCard.GamePlayerId = null;
            hiddenDealerCard.IsVisible = false;
            hiddenDealerCard.DealtAtPhase = nameof(Phases.Dealing);
            hiddenDealerCard.DealOrder = 0;
            hiddenDealerCard.DealtAt = now;
        }

        var firstSelectorSeat = FindFirstActivePlayerAfterDealer(game, eligiblePlayers);
        game.CurrentDrawPlayerIndex = firstSelectorSeat;
        game.CurrentPlayerIndex = firstSelectorSeat;
        game.CurrentPhase = nameof(Phases.DrawPhase);
        game.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
    }

    protected override async Task SendBettingActionAsync(AutoActionContext context, Data.Entities.BettingActionType action, int amount = 0)
    {
        var command = new Features.Games.HoldEm.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand(
            context.GameId,
            action,
            amount);

        try
        {
            await context.Mediator.Send(command, context.CancellationToken);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Error performing auto-betting action for Bob Barker game {GameId}", context.GameId);
        }
    }
}