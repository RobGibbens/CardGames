using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Five Card Draw poker.
/// </summary>
/// <remarks>
/// <para>
/// Five Card Draw is a classic poker variant with the following flow:
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
/// This handler uses the default phase progression from <see cref="BaseGameFlowHandler"/>
/// as the Five Card Draw rules define a linear phase sequence.
/// </para>
/// </remarks>
public sealed class FiveCardDrawFlowHandler : BaseGameFlowHandler
{
    /// <inheritdoc />
    public override string GameTypeCode => "FIVECARDDRAW";

    /// <inheritdoc />
    public override GameRules GetGameRules() => FiveCardDrawRules.CreateGameRules();

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

        // Use default linear phase progression
        return base.GetNextPhase(game, currentPhase);
    }
}
