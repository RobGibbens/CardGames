using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.CrazyPineapple;

/// <summary>
/// Crazy Pineapple:
/// deal 3 hole cards, deal flop, discard 1, then continue like Hold 'Em.
/// </summary>
[PokerGameMetadata(
    code: "CRAZYPINEAPPLE",
    name: "Crazy Pineapple",
    description: "Deal 3 hole cards, complete pre-flop betting, deal the flop, discard 1 card, then continue with Hold 'Em streets.",
    minimumNumberOfPlayers: 2,
    maximumNumberOfPlayers: 10,
    initialHoleCards: 3,
    initialBoardCards: 0,
    maxCommunityCards: 5,
    maxPlayerCards: 3,
    hasDrawPhase: true,
    maxDiscards: 1,
    wildCardRule: WildCardRule.None,
    bettingStructure: BettingStructure.Blinds,
    imageName: "crazypineapple.png")]
public sealed class CrazyPineappleGame : IPokerGame
{
    public string Name { get; } = "Crazy Pineapple";
    public string Description { get; } = "Deal 3 hole cards, complete pre-flop betting, deal the flop, discard 1 card, then continue with Hold 'Em streets.";
    public CardGames.Poker.Games.VariantType VariantType { get; } = CardGames.Poker.Games.VariantType.HoldEm;
    public int MinimumNumberOfPlayers { get; } = 2;
    public int MaximumNumberOfPlayers { get; } = 10;

    public GameFlow.GameRules GetGameRules()
    {
        return CrazyPineappleRules.CreateGameRules();
    }
}
