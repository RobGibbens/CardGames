using CardGames.Poker.Shared.DTOs.RuleSets;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.Validation;

namespace CardGames.Poker.Shared.RuleSets;

/// <summary>
/// Provides predefined rulesets for common poker variants.
/// </summary>
public static class PredefinedRuleSets
{
    /// <summary>
    /// Gets the Texas Hold'em ruleset.
    /// </summary>
    public static RuleSetDto TexasHoldem { get; } = CreateTexasHoldem();

    /// <summary>
    /// Gets the Omaha ruleset.
    /// </summary>
    public static RuleSetDto Omaha { get; } = CreateOmaha();

    /// <summary>
    /// Gets all predefined rulesets.
    /// </summary>
    public static IReadOnlyDictionary<PokerVariant, RuleSetDto> All { get; } = new Dictionary<PokerVariant, RuleSetDto>
    {
        { PokerVariant.TexasHoldem, TexasHoldem },
        { PokerVariant.Omaha, Omaha }
    };

    /// <summary>
    /// Gets a predefined ruleset by variant.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The ruleset for the variant, or null if not found.</returns>
    public static RuleSetDto? GetByVariant(PokerVariant variant)
    {
        return All.GetValueOrDefault(variant);
    }

    private static RuleSetDto CreateTexasHoldem()
    {
        var ruleSet = new RuleSetDto(
            SchemaVersion: RuleSetValidator.CurrentSchemaVersion,
            Id: "texas-holdem-nolimit",
            Name: "Texas Hold'em (No Limit)",
            Variant: PokerVariant.TexasHoldem,
            DeckComposition: new DeckCompositionDto(
                DeckType: DeckType.Full52,
                NumberOfDecks: 1
            ),
            CardVisibility: new CardVisibilityDto(
                HoleCardsPrivate: true,
                CommunityCardsPublic: true
            ),
            BettingRounds:
            [
                new BettingRoundDto(
                    Name: "Preflop",
                    Order: 0,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 2,
                    MinBetMultiplier: 1.0m
                ),
                new BettingRoundDto(
                    Name: "Flop",
                    Order: 1,
                    CommunityCardsDealt: 3,
                    HoleCardsDealt: 0,
                    MinBetMultiplier: 1.0m
                ),
                new BettingRoundDto(
                    Name: "Turn",
                    Order: 2,
                    CommunityCardsDealt: 1,
                    HoleCardsDealt: 0,
                    MinBetMultiplier: 1.0m
                ),
                new BettingRoundDto(
                    Name: "River",
                    Order: 3,
                    CommunityCardsDealt: 1,
                    HoleCardsDealt: 0,
                    MinBetMultiplier: 1.0m
                )
            ],
            HoleCardRules: new HoleCardRulesDto(
                Count: 2,
                MinUsedInHand: 0,
                MaxUsedInHand: 2,
                AllowDraw: false,
                MaxDrawCount: 0
            ),
            CommunityCardRules: new CommunityCardRulesDto(
                TotalCount: 5,
                MinUsedInHand: 0,
                MaxUsedInHand: 5
            ),
            AnteBlindRules: new AnteBlindRulesDto(
                HasAnte: false,
                AntePercentage: 0,
                HasSmallBlind: true,
                HasBigBlind: true,
                AllowStraddle: true,
                ButtonAnte: false
            ),
            LimitType: LimitType.NoLimit,
            WildcardRules: null,
            ShowdownRules: new ShowdownRulesDto(
                ShowOrder: ShowdownOrder.LastAggressor,
                AllowMuck: true,
                ShowAllOnAllIn: true
            ),
            HiLoRules: null,
            SpecialRules: null,
            Description: "The most popular poker variant. Each player receives 2 hole cards and uses them with 5 community cards to make the best 5-card hand."
        );

        return ruleSet;
    }

    private static RuleSetDto CreateOmaha()
    {
        var ruleSet = new RuleSetDto(
            SchemaVersion: RuleSetValidator.CurrentSchemaVersion,
            Id: "omaha-potlimit",
            Name: "Omaha (Pot Limit)",
            Variant: PokerVariant.Omaha,
            DeckComposition: new DeckCompositionDto(
                DeckType: DeckType.Full52,
                NumberOfDecks: 1
            ),
            CardVisibility: new CardVisibilityDto(
                HoleCardsPrivate: true,
                CommunityCardsPublic: true
            ),
            BettingRounds:
            [
                new BettingRoundDto(
                    Name: "Preflop",
                    Order: 0,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 4,
                    MinBetMultiplier: 1.0m
                ),
                new BettingRoundDto(
                    Name: "Flop",
                    Order: 1,
                    CommunityCardsDealt: 3,
                    HoleCardsDealt: 0,
                    MinBetMultiplier: 1.0m
                ),
                new BettingRoundDto(
                    Name: "Turn",
                    Order: 2,
                    CommunityCardsDealt: 1,
                    HoleCardsDealt: 0,
                    MinBetMultiplier: 1.0m
                ),
                new BettingRoundDto(
                    Name: "River",
                    Order: 3,
                    CommunityCardsDealt: 1,
                    HoleCardsDealt: 0,
                    MinBetMultiplier: 1.0m
                )
            ],
            HoleCardRules: new HoleCardRulesDto(
                Count: 4,
                MinUsedInHand: 2,
                MaxUsedInHand: 2,
                AllowDraw: false,
                MaxDrawCount: 0
            ),
            CommunityCardRules: new CommunityCardRulesDto(
                TotalCount: 5,
                MinUsedInHand: 3,
                MaxUsedInHand: 3
            ),
            AnteBlindRules: new AnteBlindRulesDto(
                HasAnte: false,
                AntePercentage: 0,
                HasSmallBlind: true,
                HasBigBlind: true,
                AllowStraddle: true,
                ButtonAnte: false
            ),
            LimitType: LimitType.PotLimit,
            WildcardRules: null,
            ShowdownRules: new ShowdownRulesDto(
                ShowOrder: ShowdownOrder.LastAggressor,
                AllowMuck: true,
                ShowAllOnAllIn: true
            ),
            HiLoRules: null,
            SpecialRules: null,
            Description: "Omaha poker. Each player receives 4 hole cards and must use exactly 2 of them with exactly 3 community cards to make the best 5-card hand."
        );

        return ruleSet;
    }
}
