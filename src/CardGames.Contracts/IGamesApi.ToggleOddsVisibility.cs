using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

public partial interface IGamesApi
{
	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Put("/api/v1/games/{gameId}/settings/odds-visibility")]
	Task<IApiResponse<ToggleOddsVisibilityResponse>> ToggleOddsVisibilityAsync(
		Guid gameId,
		[Body] ToggleOddsVisibilityRequest body,
		CancellationToken cancellationToken = default);
}