namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when a showdown is performed.
/// </summary>
public record ShowdownPerformed(
	Guid GameId,
	Guid HandId,
	bool WonByFold,
	List<ShowdownPlayerEventResult> Results,
	DateTime PerformedAt
);

/// <summary>
/// Result for a single player in the showdown event.
/// </summary>
public record ShowdownPlayerEventResult(
	Guid PlayerId,
	string PlayerName,
	List<string> Hand,
	string HandType,
	string HandDescription,
	int Payout,
	bool IsWinner,
	int FinalChipStack
);
