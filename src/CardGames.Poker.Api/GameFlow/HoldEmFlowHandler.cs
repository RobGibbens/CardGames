using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.HoldEm;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Texas Hold 'Em poker.
/// </summary>
public sealed class HoldEmFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => "HOLDEM";

    public override GameRules GetGameRules() => HoldEmRules.CreateGameRules();

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
        
        // Sort players by seat position for consistent ordering
        var sortedPlayers = eligiblePlayers.OrderBy(p => p.SeatPosition).ToList();
        
        // Find Dealer index
        var dealerIndex = sortedPlayers.FindIndex(p => p.SeatPosition == dealerPos);
        if (dealerIndex == -1)
        {
            // Fallback if dealer not found in eligible players (active players)
            // Start from the beginning
            dealerIndex = sortedPlayers.Count - 1;
        }

        GamePlayer sbPlayer;
        GamePlayer bbPlayer;

        if (sortedPlayers.Count == 2)
        {
            // Heads up: Dealer is SB, Other is BB
            sbPlayer = sortedPlayers[dealerIndex];
            bbPlayer = sortedPlayers[(dealerIndex + 1) % sortedPlayers.Count];
        }
        else
        {
            // Normal play: Dealer -> SB -> BB
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
