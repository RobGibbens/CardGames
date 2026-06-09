using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.Razz;

[PokerGameMetadata(
	"RAZZ",
	"Razz",
	"Seven Card Stud lowball where the lowest five-card hand wins. Aces are low, and straights and flushes do not count against low.",
	2,
	7,
	2,
	1,
	0,
	7,
	false,
	0,
	WildCardRule.None,
	BettingStructure.AnteBringIn,
	"razz.png")]
public sealed class RazzGame : IPokerGame
{
	public string Name { get; } = "Razz";

	public string Description { get; } =
		"Seven Card Stud lowball where the lowest five-card hand wins. Aces are low, and straights and flushes do not count against low.";

	public VariantType VariantType { get; } = VariantType.Stud;
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 7;

	public GameRules GetGameRules()
	{
		return RazzRules.CreateGameRules();
	}
}