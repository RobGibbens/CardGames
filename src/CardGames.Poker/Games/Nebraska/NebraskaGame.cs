using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.Nebraska;

[PokerGameMetadata(
    code: "NEBRASKA",
    name: "Nebraska",
    description: "A community card poker variant like Omaha where players are dealt five hole cards and must use exactly three hole cards and two community cards.",
    minimumNumberOfPlayers: 2,
    maximumNumberOfPlayers: 10,
    initialHoleCards: 5,
    initialBoardCards: 0,
    maxCommunityCards: 5,
    maxPlayerCards: 5,
    hasDrawPhase: false,
    maxDiscards: 0,
    wildCardRule: WildCardRule.None,
    bettingStructure: BettingStructure.Blinds,
    imageName: "nebraska.png")]
public sealed class NebraskaGame : IPokerGame
{
    public string Name { get; } = "Nebraska";

    public string Description { get; } = "A community card poker variant like Omaha where players are dealt five hole cards and must use exactly three hole cards and two community cards.";

    public VariantType VariantType { get; } = VariantType.HoldEm;

    public int MinimumNumberOfPlayers { get; } = 2;

    public int MaximumNumberOfPlayers { get; } = 10;

    public GameFlow.GameRules GetGameRules() => NebraskaRules.CreateGameRules();
}
