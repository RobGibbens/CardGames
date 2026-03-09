using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.RedRiver;

/// <summary>
/// Red River is a Hold 'Em-style variant with a conditional sixth community card.
/// If the river card is a heart or diamond, one additional community card is dealt.
/// </summary>
[PokerGameMetadata(
    code: "REDRIVER",
    name: "Red River",
    description: "Texas Hold 'Em with a conditional extra board card when the river is a heart or diamond.",
    minimumNumberOfPlayers: 2,
    maximumNumberOfPlayers: 10,
    initialHoleCards: 2,
    initialBoardCards: 0,
    maxCommunityCards: 6,
    maxPlayerCards: 2,
    hasDrawPhase: false,
    maxDiscards: 0,
    wildCardRule: WildCardRule.None,
    bettingStructure: BettingStructure.Blinds,
    imageName: "redriver.png")]
public sealed class RedRiverGame : IPokerGame
{
    public string Name { get; } = "Red River";
    public string Description { get; } = "Texas Hold 'Em with a conditional extra board card when the river is a heart or diamond.";
    public CardGames.Poker.Games.VariantType VariantType { get; } = CardGames.Poker.Games.VariantType.HoldEm;
    public int MinimumNumberOfPlayers { get; } = 2;
    public int MaximumNumberOfPlayers { get; } = 10;

    public GameFlow.GameRules GetGameRules()
    {
        return RedRiverRules.CreateGameRules();
    }
}
