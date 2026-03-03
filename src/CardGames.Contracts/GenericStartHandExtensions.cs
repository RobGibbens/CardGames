using Refit;

namespace CardGames.Poker.Api.Clients;

/// <summary>
/// Extends the generated IGamesApi with the generic StartHand endpoint.
/// Used by Dealer's Choice to start a hand without specifying a game type.
/// Will be auto-generated when Refit is regenerated.
/// </summary>
public partial interface IGamesApi
{
	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/generic/{gameId}/hands")]
	Task<IApiResponse> GenericStartHandAsync(Guid gameId, CancellationToken cancellationToken = default);
}
