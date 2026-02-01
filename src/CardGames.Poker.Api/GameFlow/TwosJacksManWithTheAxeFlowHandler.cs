using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.TwosJacksManWithTheAxe;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Twos, Jacks, Man with the Axe poker.
/// </summary>
/// <remarks>
/// <para>
/// Twos, Jacks, Man with the Axe is a five-card draw variant where:
/// </para>
/// <list type="bullet">
///   <item><description>All 2s are wild</description></item>
///   <item><description>All Jacks are wild</description></item>
///   <item><description>The King of Diamonds ("Man with the Axe") is wild</description></item>
/// </list>
/// <para>
/// The game flow follows standard five-card draw:
/// </para>
/// <list type="number">
///   <item><description>Collect antes from all players</description></item>
///   <item><description>Deal 5 cards face-down to each player</description></item>
///   <item><description>First betting round</description></item>
///   <item><description>Draw phase (players discard and draw)</description></item>
///   <item><description>Second betting round</description></item>
///   <item><description>Showdown (if multiple players remain)</description></item>
/// </list>
/// <para>
/// Special rule: A natural pair of 7s can split half the pot.
/// </para>
/// </remarks>
public sealed class TwosJacksManWithTheAxeFlowHandler : BaseGameFlowHandler
{
    /// <inheritdoc />
    public override string GameTypeCode => "TWOSJACKSMANWITHTHEAXE";

    /// <inheritdoc />
    public override GameRules GetGameRules() => TwosJacksManWithTheAxeRules.CreateGameRules();

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
    public override string? GetNextPhase(Game game, string currentPhase)
    {
        // Check if only one player remains - skip to showdown/complete
        if (IsSinglePlayerRemaining(game) && !IsResolutionPhase(currentPhase))
        {
            return nameof(Phases.Showdown);
        }

        // Use default linear phase progression (same as Five Card Draw)
        return base.GetNextPhase(game, currentPhase);
    }
}
