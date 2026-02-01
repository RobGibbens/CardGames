using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.KingsAndLows;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Kings and Lows poker.
/// </summary>
/// <remarks>
/// <para>
/// Kings and Lows is a unique poker variant with a non-linear flow:
/// </para>
/// <list type="number">
///   <item><description>Collect antes from all players</description></item>
///   <item><description>Deal 5 cards face-down to each player</description></item>
///   <item><description>Drop or Stay decision (players choose to fold or continue)</description></item>
///   <item><description>Based on remaining players:
///     <list type="bullet">
///       <item><description>0 players stay → Complete</description></item>
///       <item><description>1 player stays → Player vs Deck</description></item>
///       <item><description>2+ players stay → Draw Phase</description></item>
///     </list>
///   </description></item>
///   <item><description>Draw Phase → Draw Complete → Showdown</description></item>
///   <item><description>Showdown → Pot Matching (losers match the pot)</description></item>
///   <item><description>Pot Matching → Complete</description></item>
/// </list>
/// <para>
/// Wild cards: All Kings and each player's lowest card are wild.
/// </para>
/// </remarks>
public sealed class KingsAndLowsFlowHandler : BaseGameFlowHandler
{
    /// <inheritdoc />
    public override string GameTypeCode => "KINGSANDLOWS";

    /// <inheritdoc />
    public override GameRules GetGameRules() => KingsAndLowsRules.CreateGameRules();

    /// <inheritdoc />
    public override string GetInitialPhase(Game game)
    {
        // Kings and Lows: Deal first, then players decide to drop or stay.
        // Antes are only collected on the first hand; subsequent hands get pot from losers matching.
        // The background service handles the pot carryover, so we skip collecting antes.
        return nameof(Phases.Dealing);
    }

    /// <inheritdoc />
    public override string? GetNextPhase(Game game, string currentPhase)
    {
        // Kings and Lows has a unique non-linear flow based on game state
        return currentPhase switch
        {
            nameof(Phases.CollectingAntes) => nameof(Phases.Dealing),
            nameof(Phases.Dealing) => nameof(Phases.DropOrStay),
            nameof(Phases.DropOrStay) => DeterminePostDropPhase(game),
            nameof(Phases.DrawPhase) => nameof(Phases.DrawComplete),
            nameof(Phases.DrawComplete) => nameof(Phases.Showdown),
            nameof(Phases.PlayerVsDeck) => nameof(Phases.Complete),
            nameof(Phases.Showdown) => nameof(Phases.PotMatching),
            nameof(Phases.PotMatching) => DeterminePostPotMatchingPhase(game),
            _ => base.GetNextPhase(game, currentPhase)
        };
    }

    /// <inheritdoc />
    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.AllAtOnce,
            InitialCardsPerPlayer = 5,
            AllFaceDown = true
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Kings and Lows collects antes only on the very first hand. Subsequent hands
    /// get their pot from losers matching the previous pot. The background service
    /// handles pot carryover, so we skip the standard ante collection phase.
    /// </remarks>
    public override bool SkipsAnteCollection => true;

    /// <inheritdoc />
    public override IReadOnlyList<string> SpecialPhases =>
        [nameof(Phases.DropOrStay), nameof(Phases.PotMatching), nameof(Phases.PlayerVsDeck)];

    /// <inheritdoc />
    public override Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Reset all players' DropOrStay decisions and pot matching status
        foreach (var player in game.GamePlayers)
        {
            player.DropOrStayDecision = Data.Entities.DropOrStayDecision.Undecided;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines the phase to transition to after the DropOrStay phase completes.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>The next phase name based on the number of staying players.</returns>
    private static string DeterminePostDropPhase(Game game)
    {
        var stayingPlayers = game.GamePlayers
            .Count(gp => gp.Status == GamePlayerStatus.Active &&
                         !gp.HasFolded &&
                         !gp.IsSittingOut);

        return stayingPlayers switch
        {
            0 => nameof(Phases.Complete),
            1 => nameof(Phases.PlayerVsDeck),
            _ => nameof(Phases.DrawPhase)
        };
    }

    /// <summary>
    /// Determines the phase to transition to after pot matching.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>The next phase name.</returns>
    /// <remarks>
    /// In Kings and Lows, after pot matching, the game transitions to Complete.
    /// If there's a pot carryover situation (no outright winner), this could
    /// potentially loop back, but typically the hand is considered complete.
    /// </remarks>
    private static string DeterminePostPotMatchingPhase(Game game)
    {
        // After pot matching, the hand is complete
        // The pot carryover logic is handled by the game state, not phase transitions
        return nameof(Phases.Complete);
    }
}
