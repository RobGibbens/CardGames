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
	Task<IApiResponse<ProcessDrawSuccessful>> IrishHoldEmDiscardAsync(Guid gameId, [Body] IrishHoldEmDiscardRequest body, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/irish-hold-em/{gameId}/fold-during-draw")]
	Task<IApiResponse> IrishHoldEmFoldDuringDrawAsync(Guid gameId, [Body] IrishHoldEmFoldDuringDrawRequest body, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request body for the Irish Hold 'Em fold-during-draw endpoint.
/// </summary>
/// <param name="PlayerSeatIndex">The seat index of the player to fold.</param>
public record IrishHoldEmFoldDuringDrawRequest(int PlayerSeatIndex);

/// <summary>
/// Request body for the Irish Hold 'Em discard endpoint.
/// </summary>
/// <param name="DiscardIndices">Card indices to discard.</param>
/// <param name="PlayerSeatIndex">The seat index of the player discarding.</param>
public record IrishHoldEmDiscardRequest(ICollection<int> DiscardIndices, int PlayerSeatIndex);
