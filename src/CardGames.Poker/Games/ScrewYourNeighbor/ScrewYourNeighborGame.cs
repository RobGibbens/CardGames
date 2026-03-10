using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.ScrewYourNeighbor;

[PokerGameMetadata(
	code: "SCREWYOURNEIGHBOR",
	name: "Screw Your Neighbor",
	description: "Each player is dealt one card. Players decide to keep their card or trade with the player to their left. Kings are blockers. Lowest card loses a stack. Last player standing wins.",
	minimumNumberOfPlayers: 2,
	maximumNumberOfPlayers: 20,
	initialHoleCards: 1,
	initialBoardCards: 0,
	maxCommunityCards: 0,
	maxPlayerCards: 1,
	hasDrawPhase: false,
	maxDiscards: 0,
	wildCardRule: WildCardRule.None,
	bettingStructure: BettingStructure.Ante,
	imageName: "screwyourneighbor.png")]
public sealed class ScrewYourNeighborGame : IPokerGame
{
	public string Name { get; } = "Screw Your Neighbor";

	public string Description { get; } =
		"Each player is dealt one card. Players decide to keep their card or trade with the player to their left. " +
		"Kings are blockers. Lowest card loses a stack. Last player standing wins.";

	public VariantType VariantType { get; } = VariantType.Other;
	public int MinimumNumberOfPlayers { get; } = 3;
	public int MaximumNumberOfPlayers { get; } = 10;

	public GameRules GetGameRules() => ScrewYourNeighborRules.CreateGameRules();
}
