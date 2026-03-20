using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.BobBarker.v1.Commands.SelectShowcase;
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

    public override async Task PerformAutoActionAsync(AutoActionContext context)
    {
        if (IsDrawingPhase(context.CurrentPhase) && context.PlayerSeatIndex >= 0)
        {
            await AutoSelectShowcaseCardAsync(context, context.PlayerSeatIndex);

            return;
        }

        if (IsDrawingPhase(context.CurrentPhase) && context.PlayerSeatIndex < 0)
        {
            var pendingPlayers = await context.DbContext.GamePlayers
                .AsNoTracking()
                .Where(gp => gp.GameId == context.GameId
                    && gp.Status == GamePlayerStatus.Active
                    && !gp.HasFolded
                    && !gp.HasDrawnThisRound)
                .OrderBy(gp => gp.SeatPosition)
                .Select(gp => gp.SeatPosition)
                .ToListAsync(context.CancellationToken);

            foreach (var seatPosition in pendingPlayers)
            {
                await AutoSelectShowcaseCardAsync(context, seatPosition);
            }

            return;
        }

        await base.PerformAutoActionAsync(context);
    }

    private static async Task AutoSelectShowcaseCardAsync(AutoActionContext context, int playerSeatIndex)
    {
        var currentPlayer = await context.DbContext.GamePlayers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                gp => gp.GameId == context.GameId
                    && gp.SeatPosition == playerSeatIndex
                    && gp.Status == GamePlayerStatus.Active,
                context.CancellationToken);

        if (currentPlayer is null || currentPlayer.HasFolded || currentPlayer.HasDrawnThisRound)
        {
            return;
        }

        var playerCards = await context.DbContext.GameCards
            .AsNoTracking()
            .Where(gc => gc.GameId == context.GameId
                && gc.GamePlayerId == currentPlayer.Id
                && gc.HandNumber == context.Game.CurrentHandNumber
                && gc.Location == CardLocation.Hand
                && !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .ToListAsync(context.CancellationToken);

        var showcaseCardIndex = GetAutoShowcaseCardIndex(playerCards);
        if (showcaseCardIndex < 0)
        {
            return;
        }

        context.Logger.LogInformation(
            "Bob Barker auto-action: selecting showcase card index {CardIndex} for seat {SeatIndex} in game {GameId}",
            showcaseCardIndex,
            playerSeatIndex,
            context.GameId);

        var command = new SelectShowcaseCommand(context.GameId, showcaseCardIndex, playerSeatIndex);

        try
        {
            await context.Mediator.Send(command, context.CancellationToken);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Error performing Bob Barker auto-showcase selection for game {GameId}", context.GameId);
        }
    }

    private static int GetAutoShowcaseCardIndex(IReadOnlyList<GameCard> cards)
    {
        if (cards.Count == 0)
        {
            return -1;
        }

        var groups = cards
            .Select((card, index) => new AutoShowcaseCard(index, GetAutoShowcaseRankValue(card.Symbol)))
            .GroupBy(card => card.RankValue)
            .Select(group => new AutoShowcaseGroup(
                group.Key,
                group.Count(),
                group.Min(card => card.Index)))
            .OrderBy(group => group.RankValue)
            .ThenBy(group => group.LowestIndex)
            .ToList();

        var singleton = groups.FirstOrDefault(group => group.Count == 1);
        if (singleton is not null)
        {
            return singleton.LowestIndex;
        }

        return groups
            .OrderBy(group => group.Count)
            .ThenByDescending(group => group.RankValue)
            .ThenBy(group => group.LowestIndex)
            .First()
            .LowestIndex;
    }

    private static int GetAutoShowcaseRankValue(CardSymbol symbol)
    {
        return symbol switch
        {
            CardSymbol.Ace => 1,
            _ => (int)symbol
        };
    }

    private sealed record AutoShowcaseCard(int Index, int RankValue);

    private sealed record AutoShowcaseGroup(int RankValue, int Count, int LowestIndex);
}