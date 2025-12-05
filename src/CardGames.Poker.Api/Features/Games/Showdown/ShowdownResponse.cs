namespace CardGames.Poker.Api.Features.Games.Showdown;

/// <summary>
/// Response returned after performing a showdown.
/// </summary>
public record ShowdownResponse(
	bool Success,
	bool WonByFold,
	List<ShowdownPlayerResponse> Results
);

/// <summary>
/// Result for a single player in the showdown.
/// </summary>
public record ShowdownPlayerResponse(
	Guid PlayerId,
	string PlayerName,
	List<string> Hand,
	string HandType,
	string HandDescription,
	int Payout,
	bool IsWinner
);
