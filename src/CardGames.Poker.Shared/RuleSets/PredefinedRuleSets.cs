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
    /// Gets the Seven Card Stud ruleset.
    /// </summary>
    public static RuleSetDto SevenCardStud { get; } = CreateSevenCardStud();

    /// <summary>
    /// Gets the Five Card Draw ruleset.
    /// </summary>
    public static RuleSetDto FiveCardDraw { get; } = CreateFiveCardDraw();

    /// <summary>
    /// Gets the Follow the Queen ruleset.
    /// </summary>
    public static RuleSetDto FollowTheQueen { get; } = CreateFollowTheQueen();

    /// <summary>
    /// Gets all predefined rulesets.
    /// </summary>
    public static IReadOnlyDictionary<PokerVariant, RuleSetDto> All { get; } = new Dictionary<PokerVariant, RuleSetDto>
    {
        { PokerVariant.TexasHoldem, TexasHoldem },
        { PokerVariant.Omaha, Omaha },
        { PokerVariant.SevenCardStud, SevenCardStud },
        { PokerVariant.FiveCardDraw, FiveCardDraw },
        { PokerVariant.FollowTheQueen, FollowTheQueen }
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

    private static RuleSetDto CreateSevenCardStud()
    {
        var ruleSet = new RuleSetDto(
            SchemaVersion: RuleSetValidator.CurrentSchemaVersion,
            Id: "seven-card-stud-limit",
            Name: "Seven Card Stud (Limit)",
            Variant: PokerVariant.SevenCardStud,
            DeckComposition: new DeckCompositionDto(
                DeckType: DeckType.Full52,
                NumberOfDecks: 1
            ),
            CardVisibility: new CardVisibilityDto(
                HoleCardsPrivate: true,
                CommunityCardsPublic: false,
                FaceDownIndices: [0, 1, 6],
                FaceUpIndices: [2, 3, 4, 5]
            ),
            BettingRounds:
            [
                new BettingRoundDto(
                    Name: "Third Street",
                    Order: 0,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 3,
                    DealtFaceUp: false,
                    MinBetMultiplier: 0.5m
                ),
                new BettingRoundDto(
                    Name: "Fourth Street",
                    Order: 1,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 1,
                    DealtFaceUp: true,
                    MinBetMultiplier: 0.5m
                ),
                new BettingRoundDto(
                    Name: "Fifth Street",
                    Order: 2,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 1,
                    DealtFaceUp: true,
                    MinBetMultiplier: 1.0m
                ),
                new BettingRoundDto(
                    Name: "Sixth Street",
                    Order: 3,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 1,
                    DealtFaceUp: true,
                    MinBetMultiplier: 1.0m
                ),
                new BettingRoundDto(
                    Name: "Seventh Street",
                    Order: 4,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 1,
                    DealtFaceUp: false,
                    MinBetMultiplier: 1.0m
                )
            ],
            HoleCardRules: new HoleCardRulesDto(
                Count: 7,
                MinUsedInHand: 5,
                MaxUsedInHand: 5,
                AllowDraw: false,
                MaxDrawCount: 0
            ),
            CommunityCardRules: null,
            AnteBlindRules: new AnteBlindRulesDto(
                HasAnte: true,
                AntePercentage: 10,
                HasSmallBlind: false,
                HasBigBlind: false,
                AllowStraddle: false,
                ButtonAnte: false
            ),
            LimitType: LimitType.FixedLimit,
            WildcardRules: null,
            ShowdownRules: new ShowdownRulesDto(
                ShowOrder: ShowdownOrder.LastAggressor,
                AllowMuck: true,
                ShowAllOnAllIn: true
            ),
            HiLoRules: null,
            SpecialRules:
            [
                new SpecialRuleDto(
                    Id: "bring-in",
                    Name: "Bring-In",
                    Description: "The player with the lowest upcard on third street must post a forced bring-in bet.",
                    Enabled: true
                )
            ],
            Description: "A classic stud poker variant. Each player receives 7 cards (3 face-down, 4 face-up) and makes the best 5-card hand. Uses antes and bring-in instead of blinds."
        );

        return ruleSet;
    }

    private static RuleSetDto CreateFiveCardDraw()
    {
        var ruleSet = new RuleSetDto(
            SchemaVersion: RuleSetValidator.CurrentSchemaVersion,
            Id: "five-card-draw-limit",
            Name: "Five Card Draw (Limit)",
            Variant: PokerVariant.FiveCardDraw,
            DeckComposition: new DeckCompositionDto(
                DeckType: DeckType.Full52,
                NumberOfDecks: 1
            ),
            CardVisibility: new CardVisibilityDto(
                HoleCardsPrivate: true,
                CommunityCardsPublic: false
            ),
            BettingRounds:
            [
                new BettingRoundDto(
                    Name: "Pre-Draw",
                    Order: 0,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 5,
                    MinBetMultiplier: 1.0m
                ),
                new BettingRoundDto(
                    Name: "Post-Draw",
                    Order: 1,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 0,
                    MinBetMultiplier: 1.0m
                )
            ],
            HoleCardRules: new HoleCardRulesDto(
                Count: 5,
                MinUsedInHand: 5,
                MaxUsedInHand: 5,
                AllowDraw: true,
                MaxDrawCount: 3
            ),
            CommunityCardRules: null,
            AnteBlindRules: new AnteBlindRulesDto(
                HasAnte: true,
                AntePercentage: 10,
                HasSmallBlind: false,
                HasBigBlind: false,
                AllowStraddle: false,
                ButtonAnte: false
            ),
            LimitType: LimitType.FixedLimit,
            WildcardRules: null,
            ShowdownRules: new ShowdownRulesDto(
                ShowOrder: ShowdownOrder.LastAggressor,
                AllowMuck: true,
                ShowAllOnAllIn: true
            ),
            HiLoRules: null,
            SpecialRules:
            [
                new SpecialRuleDto(
                    Id: "draw-phase",
                    Name: "Draw Phase",
                    Description: "After the first betting round, players may discard up to 3 cards and draw new ones from the deck.",
                    Enabled: true
                )
            ],
            Description: "A classic draw poker variant. Each player receives 5 cards, then may discard up to 3 cards and draw new ones. Uses antes instead of blinds."
        );

        return ruleSet;
    }

    private static RuleSetDto CreateFollowTheQueen()
    {
        var ruleSet = new RuleSetDto(
            SchemaVersion: RuleSetValidator.CurrentSchemaVersion,
            Id: "follow-the-queen-limit",
            Name: "Follow the Queen (Limit)",
            Variant: PokerVariant.FollowTheQueen,
            DeckComposition: new DeckCompositionDto(
                DeckType: DeckType.Full52,
                NumberOfDecks: 1
            ),
            CardVisibility: new CardVisibilityDto(
                HoleCardsPrivate: true,
                CommunityCardsPublic: false,
                FaceDownIndices: [0, 1, 6],
                FaceUpIndices: [2, 3, 4, 5]
            ),
            BettingRounds:
            [
                new BettingRoundDto(
                    Name: "Third Street",
                    Order: 0,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 3,
                    DealtFaceUp: false,
                    MinBetMultiplier: 0.5m
                ),
                new BettingRoundDto(
                    Name: "Fourth Street",
                    Order: 1,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 1,
                    DealtFaceUp: true,
                    MinBetMultiplier: 0.5m
                ),
                new BettingRoundDto(
                    Name: "Fifth Street",
                    Order: 2,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 1,
                    DealtFaceUp: true,
                    MinBetMultiplier: 1.0m
                ),
                new BettingRoundDto(
                    Name: "Sixth Street",
                    Order: 3,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 1,
                    DealtFaceUp: true,
                    MinBetMultiplier: 1.0m
                ),
                new BettingRoundDto(
                    Name: "Seventh Street",
                    Order: 4,
                    CommunityCardsDealt: 0,
                    HoleCardsDealt: 1,
                    DealtFaceUp: false,
                    MinBetMultiplier: 1.0m
                )
            ],
            HoleCardRules: new HoleCardRulesDto(
                Count: 7,
                MinUsedInHand: 5,
                MaxUsedInHand: 5,
                AllowDraw: false,
                MaxDrawCount: 0
            ),
            CommunityCardRules: null,
            AnteBlindRules: new AnteBlindRulesDto(
                HasAnte: true,
                AntePercentage: 10,
                HasSmallBlind: false,
                HasBigBlind: false,
                AllowStraddle: false,
                ButtonAnte: false
            ),
            LimitType: LimitType.FixedLimit,
            WildcardRules: new WildcardRulesDto(
                Enabled: true,
                WildcardCards: ["Qh", "Qd", "Qc", "Qs"],
                Dynamic: true,
                DynamicRule: "Queens are always wild. The card following the last dealt face-up Queen (and all cards of that rank) are also wild."
            ),
            ShowdownRules: new ShowdownRulesDto(
                ShowOrder: ShowdownOrder.LastAggressor,
                AllowMuck: true,
                ShowAllOnAllIn: true
            ),
            HiLoRules: null,
            SpecialRules:
            [
                new SpecialRuleDto(
                    Id: "bring-in",
                    Name: "Bring-In",
                    Description: "The player with the lowest upcard on third street must post a forced bring-in bet.",
                    Enabled: true
                ),
                new SpecialRuleDto(
                    Id: "follow-the-queen",
                    Name: "Follow the Queen Wild",
                    Description: "When a Queen is dealt face up, the next card's rank becomes wild (in addition to Queens). If another Queen is dealt, the new following card replaces the previous wild rank.",
                    Enabled: true
                )
            ],
            Description: "A seven card stud variant where Queens are always wild, and the card following the last dealt face-up Queen (and all cards of that rank) are also wild. Uses antes and bring-in instead of blinds."
        );

        return ruleSet;
    }
}
