using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.Tollbooth;

/// <summary>
/// Tollbooth is a Seven Card Stud variant where cards for Fourth through Seventh streets
/// are acquired via a per-player Tollbooth offer instead of being dealt directly.
/// Two display cards are placed face-up on the table; each player chooses the furthest card
/// (free), the nearest card (1× ante), or the top deck card (2× ante).
/// </summary>
[PokerGameMetadata(
	code: "TOLLBOOTH",
	name: "Tollbooth",
	description: "A Seven Card Stud variant where players choose their Fourth through Seventh street cards from a Tollbooth offer: the furthest display card (free), the nearest display card (1× ante), or the top deck card (2× ante).",
	minimumNumberOfPlayers: 2,
	maximumNumberOfPlayers: 7,
	initialHoleCards: 2,
	initialBoardCards: 1,
	maxCommunityCards: 0,
	maxPlayerCards: 7,
	hasDrawPhase: false,
	maxDiscards: 0,
	wildCardRule: WildCardRule.None,
	bettingStructure: BettingStructure.AnteBringIn,
	imageName: "tollbooth.png")]
public sealed class TollboothGame : IPokerGame
{
	public string Name { get; } = "Tollbooth";

	public string Description { get; } =
		"A Seven Card Stud variant where players choose their Fourth through Seventh street cards from a Tollbooth offer: the furthest display card (free), the nearest display card (1× ante), or the top deck card (2× ante).";

	public VariantType VariantType { get; } = VariantType.Stud;

	public int MinimumNumberOfPlayers { get; } = 2;

	public int MaximumNumberOfPlayers { get; } = 7;

	public GameRules GetGameRules() => TollboothRules.CreateGameRules();
}
