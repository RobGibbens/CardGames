using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Api.Clients;

/// <summary>
/// Extends the generated IGamesApi with the Irish Hold'Em discard endpoint.
/// Irish Hold'Em has a post-flop discard phase where players discard 2 of 4 hole cards.
/// Will be auto-generated when Refit is regenerated.
/// </summary>
public partial interface IGamesApi
{
	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/irish-hold-em/{gameId}/discard")]
	Task<IApiResponse<ProcessDrawSuccessful>> IrishHoldEmDiscardAsync(Guid gameId, [Body] ProcessDrawRequest body, CancellationToken cancellationToken = default);
}
