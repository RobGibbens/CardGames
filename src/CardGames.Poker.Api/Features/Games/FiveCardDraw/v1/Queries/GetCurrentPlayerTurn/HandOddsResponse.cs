namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetCurrentPlayerTurn;

/// <summary>
/// Response containing the probability of ending with each hand type
/// for a player's current cards.
/// </summary>
public record HandOddsResponse(
	IReadOnlyList<HandTypeOdds> HandTypeProbabilities
);

/// <summary>
/// Represents the probability of achieving a specific poker hand type.
/// </summary>
public record HandTypeOdds(
	/// <summary>
	/// The type of poker hand (e.g., "OnePair", "Flush", "FullHouse").
	/// </summary>
	string HandType,
	
	/// <summary>
	/// The display name of the hand type (e.g., "One Pair", "Flush", "Full House").
	/// </summary>
	string DisplayName,
	
	/// <summary>
	/// The probability of achieving this hand type, as a decimal between 0 and 1.
	/// </summary>
	decimal Probability
);
