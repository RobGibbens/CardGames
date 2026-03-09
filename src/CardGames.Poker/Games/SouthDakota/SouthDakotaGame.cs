using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.SouthDakota;

[PokerGameMetadata(
    code: "SOUTHDAKOTA",
    name: "South Dakota",
    description: "A Nebraska-style Hold 'Em variant where players are dealt five hole cards, must use exactly three hole cards and two community cards, and play Flop (2 cards) and Turn with no River.",
    minimumNumberOfPlayers: 2,
    maximumNumberOfPlayers: 10,
    initialHoleCards: 5,
    initialBoardCards: 0,
    maxCommunityCards: 4,
    maxPlayerCards: 5,
    hasDrawPhase: false,
    maxDiscards: 0,
    wildCardRule: WildCardRule.None,
    bettingStructure: BettingStructure.Blinds,
    imageName: "southdakota.png")]
public sealed class SouthDakotaGame : IPokerGame
{
    public string Name { get; } = "South Dakota";

    public string Description { get; } = "A Nebraska-style Hold 'Em variant where players are dealt five hole cards, must use exactly three hole cards and two community cards, and play Flop (2 cards) and Turn with no River.";

    public VariantType VariantType { get; } = VariantType.HoldEm;

    public int MinimumNumberOfPlayers { get; } = 2;

    public int MaximumNumberOfPlayers { get; } = 10;

    public GameFlow.GameRules GetGameRules() => SouthDakotaRules.CreateGameRules();
}
