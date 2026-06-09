using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.SouthDakota;

[PokerGameMetadata(
	"SOUTHDAKOTA",
	"South Dakota",
	"A Nebraska-style Hold 'Em variant where players are dealt five hole cards, must use exactly three hole cards and two community cards, and play Flop (2 cards) and Turn with no River.",
	2,
	10,
	5,
	0,
	4,
	5,
	false,
	0,
	WildCardRule.None,
	BettingStructure.Blinds,
	"southdakota.png")]
public sealed class SouthDakotaGame : IPokerGame
{
	public string Name { get; } = "South Dakota";

	public string Description { get; } =
		"A Nebraska-style Hold 'Em variant where players are dealt five hole cards, must use exactly three hole cards and two community cards, and play Flop (2 cards) and Turn with no River.";

	public VariantType VariantType { get; } = VariantType.HoldEm;

	public int MinimumNumberOfPlayers { get; } = 2;

	public int MaximumNumberOfPlayers { get; } = 10;

	public GameRules GetGameRules()
	{
		return SouthDakotaRules.CreateGameRules();
	}
}