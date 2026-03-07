using Refit;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

/// <summary>
/// Extends the generated IGamesApi with the generic PerformShowdown endpoint.
/// Used for variants that don't yet have dedicated web Refit clients.
/// Will be auto-generated when Refit is regenerated.
/// </summary>
public partial interface IGamesApi
{
	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/generic/{gameId}/showdown")]
	Task<IApiResponse<PerformShowdownSuccessful>> GenericPerformShowdownAsync(Guid gameId, CancellationToken cancellationToken = default);
}
