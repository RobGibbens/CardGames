using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

public partial interface IGamesApi
{
	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/games/join-requests/pending-for-host")]
	Task<IApiResponse<IReadOnlyList<GameJoinRequestReceivedDto>>> GetPendingJoinRequestsForHostAsync(
		CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/{gameId}/join-requests/{joinRequestId}/resolve")]
	Task<IApiResponse<ResolveJoinRequestResponse>> ResolveJoinRequestAsync(
		Guid gameId,
		Guid joinRequestId,
		[Body] ResolveJoinRequestRequest body,
		CancellationToken cancellationToken = default);
}