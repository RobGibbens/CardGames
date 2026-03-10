using Refit;

namespace CardGames.Poker.Api.Clients;

public partial interface IGamesApi
{
    [Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
    [Put("/api/v1/games/{gameId}/settings/odds-visibility")]
    Task<IApiResponse<CardGames.Poker.Api.Contracts.ToggleOddsVisibilityResponse>> ToggleOddsVisibilityAsync(
        Guid gameId,
        [Body] CardGames.Poker.Api.Contracts.ToggleOddsVisibilityRequest body,
        CancellationToken cancellationToken = default);
}
