using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.ScrewYourNeighbor;

[PokerGameMetadata(
	"SCREWYOURNEIGHBOR",
	"Screw Your Neighbor",
	"Each player is dealt one card. Players decide to keep their card or trade with the player to their left. Kings are blockers. Lowest card loses a stack. Last player standing wins.",
	2,
	20,
	1,
	0,
	0,
	1,
	false,
	0,
	WildCardRule.None,
	BettingStructure.Ante,
	"screwyourneighbor.png")]
public sealed class ScrewYourNeighborGame : IPokerGame
{
	public string Name { get; } = "Screw Your Neighbor";

	public string Description { get; } =
		"Each player is dealt one card. Players decide to keep their card or trade with the player to their left. " +
		"Kings are blockers. Lowest card loses a stack. Last player standing wins.";

	public VariantType VariantType { get; } = VariantType.Other;
	public int MinimumNumberOfPlayers { get; } = 3;
	public int MaximumNumberOfPlayers { get; } = 10;

	public GameRules GetGameRules()
	{
		return ScrewYourNeighborRules.CreateGameRules();
	}
}