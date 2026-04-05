using Refit;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

/// <summary>
/// Extends the generated IGamesApi with the Rabbit Hunt endpoint.
/// </summary>
public partial interface IGamesApi
{
    [Headers("Accept: application/json, application/problem+json")]
    [Get("/api/v1/games/{gameId}/rabbit-hunt")]
    Task<IApiResponse<GetRabbitHuntSuccessful>> GetRabbitHuntAsync(Guid gameId, CancellationToken cancellationToken = default);
}