using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.SevenCardStud;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Seven Card Stud poker.
/// </summary>
/// <remarks>
/// <para>
/// Seven Card Stud is a classic stud poker variant with street-based dealing:
/// </para>
/// <list type="number">
///   <item><description>Collect antes from all players</description></item>
///   <item><description>Third Street: Deal 2 down cards and 1 up card, bring-in betting</description></item>
///   <item><description>Fourth Street: Deal 1 up card, betting round (small bet)</description></item>
///   <item><description>Fifth Street: Deal 1 up card, betting round (big bet)</description></item>
///   <item><description>Sixth Street: Deal 1 up card, betting round (big bet)</description></item>
///   <item><description>Seventh Street: Deal 1 down card, final betting round (big bet)</description></item>
///   <item><description>Showdown (if multiple players remain)</description></item>
/// </list>
/// <para>
/// Each player receives 7 cards total: 3 face-down (2 initial + river) and 4 face-up.
/// Best 5-card hand wins.
/// </para>
/// </remarks>
public sealed class SevenCardStudFlowHandler : BaseGameFlowHandler
{
    /// <inheritdoc />
    public override string GameTypeCode => "SEVENCARDSTUD";

    /// <inheritdoc />
    public override GameRules GetGameRules() => SevenCardStudRules.CreateGameRules();

    /// <inheritdoc />
    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.StreetBased,
            DealingRounds =
            [
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.ThirdStreet),
                    HoleCards = 2,
                    BoardCards = 1,
                    HasBettingAfter = true
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.FourthStreet),
                    HoleCards = 0,
                    BoardCards = 1,
                    HasBettingAfter = true
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.FifthStreet),
                    HoleCards = 0,
                    BoardCards = 1,
                    HasBettingAfter = true
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.SixthStreet),
                    HoleCards = 0,
                    BoardCards = 1,
                    HasBettingAfter = true
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.SeventhStreet),
                    HoleCards = 1,
                    BoardCards = 0,
                    HasBettingAfter = true
                }
            ]
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

        // Seven Card Stud has explicit street-based phase transitions
        return currentPhase switch
        {
            nameof(Phases.CollectingAntes) => nameof(Phases.ThirdStreet),
            nameof(Phases.ThirdStreet) => nameof(Phases.FourthStreet),
            nameof(Phases.FourthStreet) => nameof(Phases.FifthStreet),
            nameof(Phases.FifthStreet) => nameof(Phases.SixthStreet),
            nameof(Phases.SixthStreet) => nameof(Phases.SeventhStreet),
            nameof(Phases.SeventhStreet) => nameof(Phases.Showdown),
            nameof(Phases.Showdown) => nameof(Phases.Complete),
            _ => base.GetNextPhase(game, currentPhase)
        };
    }

    /// <summary>
    /// Gets the street names for Seven Card Stud.
    /// </summary>
    /// <remarks>
    /// Streets are the dealing/betting rounds in stud games:
    /// Third Street through Seventh Street correspond to the 3rd through 7th cards dealt.
    /// </remarks>
    public static IReadOnlyList<string> Streets =>
    [
        nameof(Phases.ThirdStreet),
        nameof(Phases.FourthStreet),
        nameof(Phases.FifthStreet),
        nameof(Phases.SixthStreet),
        nameof(Phases.SeventhStreet)
    ];

    /// <summary>
    /// Determines if the specified phase is a street (dealing + betting) phase.
    /// </summary>
    /// <param name="phase">The phase to check.</param>
    /// <returns>True if the phase is a street; otherwise, false.</returns>
    public static bool IsStreetPhase(string phase)
    {
        return Streets.Contains(phase);
    }
}
