namespace CardGames.Poker.Api.Features.Games.ContinueGame;

/// <summary>
/// Response returned after checking if game can continue.
/// </summary>
public record ContinueGameResponse(
	bool CanContinue,
	string Status,
	int PlayersWithChips,
	List<PlayerChipStatusResponse> Players,
	string? WinnerName = null,
	int? WinnerChips = null
);

/// <summary>
/// Player chip status for continue game response.
/// </summary>
public record PlayerChipStatusResponse(
	Guid PlayerId,
	string Name,
	int ChipStack,
	bool CanPlay
);
