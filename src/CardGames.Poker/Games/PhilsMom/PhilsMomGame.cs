using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.PhilsMom;

/// <summary>
///     Phil's Mom (Irish Hold 'Em variant):
///     deal 4 hole cards, discard 1 before flop, discard 1 after flop, then continue with turn/river.
/// </summary>
[PokerGameMetadata(
	"PHILSMOM",
	"Phil's Mom",
	"Deal 4 hole cards, discard 1 before the flop and 1 after the flop, then play Hold 'Em with community cards.",
	2,
	10,
	4,
	0,
	5,
	4,
	true,
	1,
	WildCardRule.None,
	BettingStructure.Blinds,
	"philsmom.png")]
public sealed class PhilsMomGame : IPokerGame
{
	public string Name { get; } = "Phil's Mom";

	public string Description { get; } =
		"Deal 4 hole cards, discard 1 before the flop and 1 after the flop, then play Hold 'Em with community cards.";

	public VariantType VariantType { get; } = VariantType.HoldEm;
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 10;

	public GameRules GetGameRules()
	{
		return PhilsMomRules.CreateGameRules();
	}
}