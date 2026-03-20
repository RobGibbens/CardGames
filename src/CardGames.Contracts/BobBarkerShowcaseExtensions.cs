using Refit;

namespace CardGames.Poker.Api.Clients;

public partial interface IGamesApi
{
    [Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
    [Post("/api/v1/games/bob-barker/{gameId}/showcase")]
    Task<IApiResponse> BobBarkerSelectShowcaseAsync(Guid gameId, [Body] BobBarkerSelectShowcaseRequest body, CancellationToken cancellationToken = default);
}

public record BobBarkerSelectShowcaseRequest(int ShowcaseCardIndex, int PlayerSeatIndex);