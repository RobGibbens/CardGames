using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.Tollbooth;

/// <summary>
///     Tollbooth is a Seven Card Stud variant where cards for Fourth through Seventh streets
///     are acquired via a per-player Tollbooth offer instead of being dealt directly.
///     Two display cards are placed face-up on the table; each player chooses the furthest card
///     (free), the nearest card (1× ante), or the top deck card (2× ante).
/// </summary>
[PokerGameMetadata(
	"TOLLBOOTH",
	"Tollbooth",
	"A Seven Card Stud variant where players choose their Fourth through Seventh street cards from a Tollbooth offer: the furthest display card (free), the nearest display card (1× ante), or the top deck card (2× ante).",
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
	"tollbooth.png")]
public sealed class TollboothGame : IPokerGame
{
	public string Name { get; } = "Tollbooth";

	public string Description { get; } =
		"A Seven Card Stud variant where players choose their Fourth through Seventh street cards from a Tollbooth offer: the furthest display card (free), the nearest display card (1× ante), or the top deck card (2× ante).";

	public VariantType VariantType { get; } = VariantType.Stud;

	public int MinimumNumberOfPlayers { get; } = 2;

	public int MaximumNumberOfPlayers { get; } = 7;

	public GameRules GetGameRules()
	{
		return TollboothRules.CreateGameRules();
	}
}