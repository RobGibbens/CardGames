using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.InBetween;

[PokerGameMetadata(
	"INBETWEEN",
	"In-Between",
	"Two boundary cards are dealt face-up. Players bet on whether the next card's rank falls strictly between the two boundaries. Match a boundary and you POST (pay double). Ace can be declared high or low. Game ends when the pot is empty.",
	2,
	20,
	0,
	0,
	0,
	0,
	false,
	0,
	WildCardRule.None,
	BettingStructure.Ante,
	"inbetween.png")]
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

	public GameRules GetGameRules()
	{
		return InBetweenRules.CreateGameRules();
	}
}