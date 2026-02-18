using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetCashierSummary;

public static class GetCashierSummaryEndpoint
{
	public static RouteGroupBuilder MapGetCashierSummary(this RouteGroupBuilder group)
	{
		group.MapGet("cashier/summary",
				async (IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetCashierSummaryQuery(), cancellationToken);
					return Results.Ok(response);
				})
			.WithName("GetCashierSummary")
			.WithSummary("Get cashier summary")
			.WithDescription("Retrieves the current account chip balance and latest transaction timestamp for the authenticated player.")
			.Produces<CashierSummaryDto>(StatusCodes.Status200OK)
			.RequireAuthorization();

		return group;
	}
}
