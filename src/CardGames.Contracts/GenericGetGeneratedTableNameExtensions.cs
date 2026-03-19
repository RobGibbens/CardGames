using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Api.Clients;

/// <summary>
/// Extends the generated IGamesApi with the generic generated table name endpoint.
/// Used by clients that already inject IGamesApi instead of IGenericGamesApi.
/// Will be auto-generated when Refit is regenerated.
/// </summary>
public partial interface IGamesApi
{
	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/games/generic/table-name")]
	Task<IApiResponse<GetGeneratedTableNameResponse>> GenericGetGeneratedTableNameAsync([Query] string? gameType = null, CancellationToken cancellationToken = default);
}