using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Api.Clients;

/// <summary>
/// Manual interface for Good Bad Ugly endpoints until Refit output is regenerated.
/// </summary>
public partial interface IGoodBadUglyApi
{
    [Headers("Accept: application/json, application/problem+json")]
    [Post("/api/v1/games/good-bad-ugly/{gameId}/hands/deal")]
    Task<IApiResponse<DealHandsSuccessful>> GoodBadUglyDealHandsAsync(Guid gameId, CancellationToken cancellationToken = default);

    [Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
    [Post("/api/v1/games/good-bad-ugly/{gameId}/betting/actions")]
    Task<IApiResponse<ProcessBettingActionSuccessful>> GoodBadUglyProcessBettingActionAsync(Guid gameId, [Body] ProcessBettingActionRequest body, CancellationToken cancellationToken = default);

    [Headers("Accept: application/json, application/problem+json")]
    [Post("/api/v1/games/good-bad-ugly/{gameId}/showdown")]
    Task<IApiResponse<PerformShowdownSuccessful>> GoodBadUglyPerformShowdownAsync(Guid gameId, CancellationToken cancellationToken = default);
}
