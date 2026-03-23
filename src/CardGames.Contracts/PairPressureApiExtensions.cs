using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Api.Clients;

public partial interface IPairPressureApi
{
	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/pair-pressure/{gameId}/hands")]
	Task<IApiResponse<StartHandSuccessful>> PairPressureStartHandAsync(Guid gameId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/pair-pressure/{gameId}/hands/antes")]
	Task<IApiResponse<CollectAntesSuccessful>> PairPressureCollectAntesAsync(Guid gameId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/pair-pressure/{gameId}/hands/deal")]
	Task<IApiResponse<DealHandsSuccessful>> PairPressureDealHandsAsync(Guid gameId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/pair-pressure/{gameId}/betting/actions")]
	Task<IApiResponse<ProcessBettingActionSuccessful>> PairPressureProcessBettingActionAsync(Guid gameId, [Body] ProcessBettingActionRequest body, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/pair-pressure/{gameId}/showdown")]
	Task<IApiResponse<PerformShowdownSuccessful>> PairPressurePerformShowdownAsync(Guid gameId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/games/pair-pressure/{gameId}/current-turn")]
	Task<IApiResponse<GetCurrentPlayerTurnResponse>> PairPressureGetCurrentPlayerTurnAsync(Guid gameId, CancellationToken cancellationToken = default);
}