using Refit;

namespace CardGames.Poker.Api.Clients;

/// <summary>
/// Generic Games API for retrieving games regardless of type.
/// </summary>
public partial interface IGamesApi
{
	/// <summary>
	/// Retrieves a specific game by its identifier.
	/// Works for any game type and returns the game with its type code.
	/// </summary>
	/// <param name="gameId">The unique identifier of the game.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The game details including the game type code.</returns>
	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/games/{gameId}")]
	Task<IApiResponse<GamesGetGameResponse>> GamesGetGameAsync(Guid gameId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from the generic Games API GetGame endpoint.
/// Includes GameTypeCode for routing to the correct variant-specific API.
/// </summary>
public record GamesGetGameResponse(
	Guid Id,
	Guid GameTypeId,
	string? GameTypeCode,
	string? GameTypeName,
	string? Name,
	int MinimumNumberOfPlayers,
	int MaximumNumberOfPlayers,
	string CurrentPhase,
	string? CurrentPhaseDescription,
	int CurrentHandNumber,
	int DealerPosition,
	int? Ante,
	int? SmallBlind,
	int? BigBlind,
	int? BringIn,
	int? SmallBet,
	int? BigBet,
	int? MinBet,
	string? GameSettings,
	CardGames.Poker.Api.Contracts.GameStatus Status,
	int CurrentPlayerIndex,
	int BringInPlayerIndex,
	int CurrentDrawPlayerIndex,
	int? RandomSeed,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	DateTimeOffset? StartedAt,
	DateTimeOffset? EndedAt,
	string? CreatedById,
	string? CreatedByName,
	bool CanContinue,
	string RowVersion
);
