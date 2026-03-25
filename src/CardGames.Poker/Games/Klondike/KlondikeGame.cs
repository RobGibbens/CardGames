using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.Klondike;

/// <summary>
/// Klondike Hold'em is a Texas Hold'em variant with a single hidden wild card
/// (the Klondike Card) placed face-down after the flop. At showdown, each player
/// may independently treat the Klondike Card as any rank and suit to make their
/// best 5-card hand.
/// </summary>
[PokerGameMetadata(
    code: "KLONDIKE",
    name: "Klondike Hold'em",
    description: "Texas Hold 'Em with a hidden wild card placed after the flop. At showdown, each player treats it as any rank and suit.",
    minimumNumberOfPlayers: 2,
    maximumNumberOfPlayers: 10,
    initialHoleCards: 2,
    initialBoardCards: 0,
    maxCommunityCards: 6,
    maxPlayerCards: 2,
    hasDrawPhase: false,
    maxDiscards: 0,
    wildCardRule: WildCardRule.Dynamic,
    bettingStructure: BettingStructure.Blinds,
    imageName: "klondike.png")]
public sealed class KlondikeGame : IPokerGame
{
    public string Name { get; } = "Klondike Hold'em";
    public string Description { get; } = "Texas Hold 'Em with a hidden wild card placed after the flop. At showdown, each player treats it as any rank and suit.";
    public VariantType VariantType { get; } = VariantType.HoldEm;
    public int MinimumNumberOfPlayers { get; } = 2;
    public int MaximumNumberOfPlayers { get; } = 10;

    public GameFlow.GameRules GetGameRules()
    {
        return KlondikeRules.CreateGameRules();
    }
}
