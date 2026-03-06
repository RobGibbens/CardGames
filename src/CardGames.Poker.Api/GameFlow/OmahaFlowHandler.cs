using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.Omaha;

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

}
