using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.Nebraska;

[PokerGameMetadata(
	"NEBRASKA",
	"Nebraska",
	"A community card poker variant like Omaha where players are dealt five hole cards and must use exactly three hole cards and two community cards.",
	2,
	10,
	5,
	0,
	5,
	5,
	false,
	0,
	WildCardRule.None,
	BettingStructure.Blinds,
	"nebraska.png")]
public sealed class NebraskaGame : IPokerGame
{
	public string Name { get; } = "Nebraska";

	public string Description { get; } =
		"A community card poker variant like Omaha where players are dealt five hole cards and must use exactly three hole cards and two community cards.";

	public VariantType VariantType { get; } = VariantType.HoldEm;

	public int MinimumNumberOfPlayers { get; } = 2;

	public int MaximumNumberOfPlayers { get; } = 10;

	public GameRules GetGameRules()
	{
		return NebraskaRules.CreateGameRules();
	}
}