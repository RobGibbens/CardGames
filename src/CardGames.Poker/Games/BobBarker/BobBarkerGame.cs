using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.BobBarker;

[PokerGameMetadata(
	"BOBBARKER",
	"Bob Barker",
	"Deal five hole cards, set one showcase card aside against a hidden dealer card, then play the remaining four cards like Omaha for half the pot.",
	2,
	10,
	5,
	0,
	6,
	5,
	true,
	1,
	WildCardRule.None,
	BettingStructure.Blinds,
	"bobbarker.png")]
public sealed class BobBarkerGame : IPokerGame
{
	public string Name { get; } = "Bob Barker";

	public string Description { get; } =
		"Deal five hole cards, set one showcase card aside against a hidden dealer card, then play the remaining four cards like Omaha for half the pot.";

	public VariantType VariantType { get; } = VariantType.HoldEm;

	public int MinimumNumberOfPlayers { get; } = 2;

	public int MaximumNumberOfPlayers { get; } = 10;

	public GameRules GetGameRules()
	{
		return BobBarkerRules.CreateGameRules();
	}
}