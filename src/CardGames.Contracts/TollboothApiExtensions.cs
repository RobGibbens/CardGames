using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Api.Clients;

public partial interface ITollboothApi
{
	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/tollbooth/{gameId}/hands")]
	Task<IApiResponse<StartHandSuccessful>> TollboothStartHandAsync(Guid gameId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/tollbooth/{gameId}/hands/antes")]
	Task<IApiResponse<CollectAntesSuccessful>> TollboothCollectAntesAsync(Guid gameId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/tollbooth/{gameId}/hands/deal")]
	Task<IApiResponse<DealHandsSuccessful>> TollboothDealHandsAsync(Guid gameId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/tollbooth/{gameId}/betting/actions")]
	Task<IApiResponse<ProcessBettingActionSuccessful>> TollboothProcessBettingActionAsync(Guid gameId, [Body] ProcessBettingActionRequest body, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/tollbooth/{gameId}/choose-card")]
	Task<IApiResponse<TollboothChooseCardSuccessful>> TollboothChooseCardAsync(Guid gameId, [Body] TollboothChooseCardRequest body, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/tollbooth/{gameId}/showdown")]
	Task<IApiResponse<PerformShowdownSuccessful>> TollboothPerformShowdownAsync(Guid gameId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/games/tollbooth/{gameId}/current-turn")]
	Task<IApiResponse<GetCurrentPlayerTurnResponse>> TollboothGetCurrentPlayerTurnAsync(Guid gameId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request body for the Tollbooth card selection endpoint.
/// </summary>
public class TollboothChooseCardRequest
{
	/// <summary>
	/// The choice: "Furthest", "Nearest", or "Deck".
	/// </summary>
	public string Choice { get; set; } = string.Empty;

	/// <summary>
	/// The seat index of the player making the choice (optional, inferred if omitted).
	/// </summary>
	public int? PlayerSeatIndex { get; set; }
}

/// <summary>
/// Response from the Tollbooth card selection endpoint.
/// </summary>
public class TollboothChooseCardSuccessful
{
	public Guid GameId { get; set; }
	public string PlayerName { get; set; } = string.Empty;
	public int PlayerSeatIndex { get; set; }
	public string Choice { get; set; } = string.Empty;
	public int Cost { get; set; }
	public bool OfferRoundComplete { get; set; }
	public string CurrentPhase { get; set; } = string.Empty;
	public int NextPlayerSeatIndex { get; set; }
	public string? NextPlayerName { get; set; }
}
