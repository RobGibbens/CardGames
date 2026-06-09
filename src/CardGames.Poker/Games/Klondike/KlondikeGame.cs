using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.Klondike;

/// <summary>
///     Klondike Hold'em is a Texas Hold'em variant with a single hidden wild card
///     (the Klondike Card) placed face-down after the flop. At showdown, each player
///     may independently treat the Klondike Card as any rank and suit to make their
///     best 5-card hand.
/// </summary>
[PokerGameMetadata(
	"KLONDIKE",
	"Klondike Hold'em",
	"Texas Hold 'Em with a hidden wild card placed after the flop. At showdown, each player treats it as any rank and suit.",
	2,
	10,
	2,
	0,
	6,
	2,
	false,
	0,
	WildCardRule.Dynamic,
	BettingStructure.Blinds,
	"klondike.png")]
public sealed class KlondikeGame : IPokerGame
{
	public string Name { get; } = "Klondike Hold'em";

	public string Description { get; } =
		"Texas Hold 'Em with a hidden wild card placed after the flop. At showdown, each player treats it as any rank and suit.";

	public VariantType VariantType { get; } = VariantType.HoldEm;
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 10;

	public GameRules GetGameRules()
	{
		return KlondikeRules.CreateGameRules();
	}
}