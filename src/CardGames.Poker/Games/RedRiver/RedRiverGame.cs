using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.RedRiver;

/// <summary>
///     Red River is a Hold 'Em-style variant with a conditional sixth community card.
///     If the river card is a heart or diamond, one additional community card is dealt.
/// </summary>
[PokerGameMetadata(
	"REDRIVER",
	"Red River",
	"Texas Hold 'Em with a conditional extra board card when the river is a heart or diamond.",
	2,
	10,
	2,
	0,
	6,
	2,
	false,
	0,
	WildCardRule.None,
	BettingStructure.Blinds,
	"redriver.png")]
public sealed class RedRiverGame : IPokerGame
{
	public string Name { get; } = "Red River";

	public string Description { get; } =
		"Texas Hold 'Em with a conditional extra board card when the river is a heart or diamond.";

	public VariantType VariantType { get; } = VariantType.HoldEm;
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 10;

	public GameRules GetGameRules()
	{
		return RedRiverRules.CreateGameRules();
	}
}