using Refit;

namespace CardGames.Poker.Api.Clients;

/// <summary>
/// Extends the generated IGamesApi with the Hold the Baseball start-hand endpoint.
/// Will be auto-generated when Refit clients are regenerated.
/// </summary>
public partial interface IGamesApi
{
	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/hold-the-baseball/{gameId}/start")]
	Task<IApiResponse> HoldTheBaseballStartHandAsync(Guid gameId, CancellationToken cancellationToken = default);
}
