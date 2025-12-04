namespace CardGames.Poker.Api.Features.Games.Domain;

/// <summary>
/// Configuration settings for a poker game.
/// </summary>
public record GameConfiguration(
	int Ante,
	int MinBet,
	int StartingChips,
	int MaxPlayers
)
{
	/// <summary>Default configuration for 5-Card Draw</summary>
	public static GameConfiguration DefaultFiveCardDraw => new(
		Ante: 10,
		MinBet: 20,
		StartingChips: 1000,
		MaxPlayers: 6
	);
}