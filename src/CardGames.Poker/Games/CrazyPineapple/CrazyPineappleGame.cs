using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.CrazyPineapple;

/// <summary>
///     Crazy Pineapple:
///     deal 3 hole cards, deal flop, discard 1, then continue like Hold 'Em.
/// </summary>
[PokerGameMetadata(
	"CRAZYPINEAPPLE",
	"Crazy Pineapple",
	"Deal 3 hole cards, complete pre-flop betting, deal the flop, discard 1 card, then continue with Hold 'Em streets.",
	2,
	10,
	3,
	0,
	5,
	3,
	true,
	1,
	WildCardRule.None,
	BettingStructure.Blinds,
	"crazypineapple.png")]
public sealed class CrazyPineappleGame : IPokerGame
{
	public string Name { get; } = "Crazy Pineapple";

	public string Description { get; } =
		"Deal 3 hole cards, complete pre-flop betting, deal the flop, discard 1 card, then continue with Hold 'Em streets.";

	public VariantType VariantType { get; } = VariantType.HoldEm;
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 10;

	public GameRules GetGameRules()
	{
		return CrazyPineappleRules.CreateGameRules();
	}
}