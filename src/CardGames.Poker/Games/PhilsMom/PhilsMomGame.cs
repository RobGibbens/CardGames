using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.PhilsMom;

/// <summary>
/// Phil's Mom (Irish Hold 'Em variant):
/// deal 4 hole cards, discard 1 before flop, discard 1 after flop, then continue with turn/river.
/// </summary>
[PokerGameMetadata(
    code: "PHILSMOM",
    name: "Phil's Mom",
    description: "Deal 4 hole cards, discard 1 before the flop and 1 after the flop, then play Hold 'Em with community cards.",
    minimumNumberOfPlayers: 2,
    maximumNumberOfPlayers: 10,
    initialHoleCards: 4,
    initialBoardCards: 0,
    maxCommunityCards: 5,
    maxPlayerCards: 4,
    hasDrawPhase: true,
    maxDiscards: 1,
    wildCardRule: WildCardRule.None,
    bettingStructure: BettingStructure.Blinds,
    imageName: "philsmom.png")]
public sealed class PhilsMomGame : IPokerGame
{
    public string Name { get; } = "Phil's Mom";
    public string Description { get; } = "Deal 4 hole cards, discard 1 before the flop and 1 after the flop, then play Hold 'Em with community cards.";
    public CardGames.Poker.Games.VariantType VariantType { get; } = CardGames.Poker.Games.VariantType.HoldEm;
    public int MinimumNumberOfPlayers { get; } = 2;
    public int MaximumNumberOfPlayers { get; } = 10;

    public GameFlow.GameRules GetGameRules()
    {
        return PhilsMomRules.CreateGameRules();
    }
}
