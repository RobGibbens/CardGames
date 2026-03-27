using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.InBetween;

[PokerGameMetadata(
	code: "INBETWEEN",
	name: "In-Between",
	description: "Two boundary cards are dealt face-up. Players bet on whether the next card's rank falls strictly between the two boundaries. Match a boundary and you POST (pay double). Ace can be declared high or low. Game ends when the pot is empty.",
	minimumNumberOfPlayers: 2,
	maximumNumberOfPlayers: 20,
	initialHoleCards: 0,
	initialBoardCards: 0,
	maxCommunityCards: 0,
	maxPlayerCards: 0,
	hasDrawPhase: false,
	maxDiscards: 0,
	wildCardRule: WildCardRule.None,
	bettingStructure: BettingStructure.Ante,
	imageName: "inbetween.png")]
public sealed class InBetweenGame : IPokerGame
{
	public string Name { get; } = "In-Between";

	public string Description { get; } =
		"Two boundary cards are dealt face-up. Players bet on whether the next card's rank falls strictly between " +
		"the two boundaries. Match a boundary and you POST (pay double). Ace can be declared high or low. " +
		"Game ends when the pot is empty.";

	public VariantType VariantType { get; } = VariantType.Other;
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 20;

	public GameRules GetGameRules() => InBetweenRules.CreateGameRules();
}
