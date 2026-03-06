using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetCashierLedger;

public static class GetCashierLedgerEndpoint
{
	public static RouteGroupBuilder MapGetCashierLedger(this RouteGroupBuilder group)
	{
		group.MapGet("cashier/ledger",
              async (
					IMediator mediator,
					int? pageSize = null,
					int? pageNumber = null,
					int? take = null,
					int? skip = null,
					CancellationToken cancellationToken = default) =>
				{
                    var resolvedPageSize = Math.Clamp(pageSize ?? take ?? 10, 1, 100);
					var resolvedPageNumber = pageNumber ?? 1;

					if (!pageNumber.HasValue && skip.HasValue)
					{
						resolvedPageNumber = Math.Max(1, (skip.Value / resolvedPageSize) + 1);
					}

					var response = await mediator.Send(new GetCashierLedgerQuery(resolvedPageSize, resolvedPageNumber), cancellationToken);
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
