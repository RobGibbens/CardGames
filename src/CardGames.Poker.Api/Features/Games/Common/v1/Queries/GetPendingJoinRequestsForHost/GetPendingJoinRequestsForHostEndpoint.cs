using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetPendingJoinRequestsForHost;

public static class GetPendingJoinRequestsForHostEndpoint
{
	public static RouteGroupBuilder MapGetPendingJoinRequestsForHost(this RouteGroupBuilder group)
	{
		group.MapGet("join-requests/pending-for-host",
				async (IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetPendingJoinRequestsForHostQuery(), cancellationToken);
					return Results.Ok(response);
				})
			.WithName($"{nameof(MapGetPendingJoinRequestsForHost).TrimPrefix("Map")}")
			.WithSummary("Get Pending Join Requests For Host")
			.Produces(StatusCodes.Status200OK);

		return group;
	}
}