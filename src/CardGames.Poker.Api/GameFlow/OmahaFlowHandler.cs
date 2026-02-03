using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.Omaha;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Omaha poker.
/// </summary>
public sealed class OmahaFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => "OMAHA";

    public override GameRules GetGameRules() => OmahaRules.CreateGameRules();

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
        return "Dealing";
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

    private async Task CollectBlindsAsync(
        CardsDbContext context,
        Game game,
        List<GamePlayer> eligiblePlayers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (eligiblePlayers.Count < 2) return;

        var sbAmount = game.SmallBlind ?? 0;
        var bbAmount = game.BigBlind ?? 0;

        if (sbAmount == 0 && bbAmount == 0) return;

        var dealerPos = game.DealerPosition;
        var sortedPlayers = eligiblePlayers.OrderBy(p => p.SeatPosition).ToList();
        
        var dealerIndex = sortedPlayers.FindIndex(p => p.SeatPosition == dealerPos);
        if (dealerIndex == -1) dealerIndex = sortedPlayers.Count - 1;

        GamePlayer sbPlayer;
        GamePlayer bbPlayer;

        if (sortedPlayers.Count == 2)
        {
            sbPlayer = sortedPlayers[dealerIndex];
            bbPlayer = sortedPlayers[(dealerIndex + 1) % sortedPlayers.Count];
        }
        else
        {
            sbPlayer = sortedPlayers[(dealerIndex + 1) % sortedPlayers.Count];
            bbPlayer = sortedPlayers[(dealerIndex + 2) % sortedPlayers.Count];
        }

        await PostBlindAsync(context, game, sbPlayer, sbAmount, now);
        await PostBlindAsync(context, game, bbPlayer, bbAmount, now);
    }

    private async Task PostBlindAsync(CardsDbContext context, Game game, GamePlayer player, int amount, DateTimeOffset now)
    {
        if (amount <= 0) return;

        var actualAmount = Math.Min(amount, player.ChipStack);
        player.ChipStack -= actualAmount;
        player.CurrentBet += actualAmount;
        player.TotalContributedThisHand += actualAmount;

        var pot = await context.Pots
            .FirstOrDefaultAsync(p => p.GameId == game.Id &&
                                      p.HandNumber == game.CurrentHandNumber &&
                                      p.PotType == PotType.Main);

        if (pot != null)
        {
            pot.Amount += actualAmount;
        }
    }
}
