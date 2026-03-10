using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.Razz;

[PokerGameMetadata(
    code: "RAZZ",
    name: "Razz",
    description: "Seven Card Stud lowball where the lowest five-card hand wins. Aces are low, and straights and flushes do not count against low.",
    minimumNumberOfPlayers: 2,
    maximumNumberOfPlayers: 7,
    initialHoleCards: 2,
    initialBoardCards: 1,
    maxCommunityCards: 0,
    maxPlayerCards: 7,
    hasDrawPhase: false,
    maxDiscards: 0,
    wildCardRule: WildCardRule.None,
    bettingStructure: BettingStructure.AnteBringIn,
    imageName: "razz.png")]
public sealed class RazzGame : IPokerGame
{
    public string Name { get; } = "Razz";
    public string Description { get; } = "Seven Card Stud lowball where the lowest five-card hand wins. Aces are low, and straights and flushes do not count against low.";
    public VariantType VariantType { get; } = VariantType.Stud;
    public int MinimumNumberOfPlayers { get; } = 2;
    public int MaximumNumberOfPlayers { get; } = 7;

    public GameFlow.GameRules GetGameRules()
    {
        return RazzRules.CreateGameRules();
    }
}
