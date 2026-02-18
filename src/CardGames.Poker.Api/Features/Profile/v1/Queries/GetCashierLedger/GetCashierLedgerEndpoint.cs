using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetCashierLedger;

public static class GetCashierLedgerEndpoint
{
	public static RouteGroupBuilder MapGetCashierLedger(this RouteGroupBuilder group)
	{
		group.MapGet("cashier/ledger",
				async (IMediator mediator, int take = 25, int skip = 0, CancellationToken cancellationToken = default) =>
				{
					var response = await mediator.Send(new GetCashierLedgerQuery(take, skip), cancellationToken);
					return Results.Ok(response);
				})
			.WithName("GetCashierLedger")
			.WithSummary("Get cashier ledger")
			.WithDescription("Retrieves a paged, newest-first ledger of account chip transactions for the authenticated player.")
			.Produces<CashierLedgerPageDto>(StatusCodes.Status200OK)
			.RequireAuthorization();

		return group;
	}
}
