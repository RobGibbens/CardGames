using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.BobBarker;

[PokerGameMetadata(
    code: "BOBBARKER",
    name: "Bob Barker",
    description: "Deal five hole cards, set one showcase card aside against a hidden dealer card, then play the remaining four cards like Omaha for half the pot.",
    minimumNumberOfPlayers: 2,
    maximumNumberOfPlayers: 10,
    initialHoleCards: 5,
    initialBoardCards: 0,
    maxCommunityCards: 6,
    maxPlayerCards: 5,
    hasDrawPhase: true,
    maxDiscards: 1,
    wildCardRule: WildCardRule.None,
    bettingStructure: BettingStructure.Blinds,
    imageName: "bobbarker.png")]
public sealed class BobBarkerGame : IPokerGame
{
    public string Name { get; } = "Bob Barker";

    public string Description { get; } = "Deal five hole cards, set one showcase card aside against a hidden dealer card, then play the remaining four cards like Omaha for half the pot.";

    public VariantType VariantType { get; } = VariantType.HoldEm;

    public int MinimumNumberOfPlayers { get; } = 2;

    public int MaximumNumberOfPlayers { get; } = 10;

    public GameFlow.GameRules GetGameRules() => BobBarkerRules.CreateGameRules();
}