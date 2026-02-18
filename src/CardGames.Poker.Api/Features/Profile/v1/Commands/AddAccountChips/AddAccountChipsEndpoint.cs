using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.AddAccountChips;

public static class AddAccountChipsEndpoint
{
	public static RouteGroupBuilder MapAddAccountChips(this RouteGroupBuilder group)
	{
		group.MapPost("cashier/add-chips",
				async (AddAccountChipsRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new AddAccountChipsCommand(request.Amount, request.Reason), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							AddAccountChipsErrorCode.Unauthorized => Results.Unauthorized(),
							AddAccountChipsErrorCode.InvalidAmount => Results.BadRequest(new { error.Message }),
							AddAccountChipsErrorCode.PlayerUnavailable => Results.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("AddAccountChips")
			.WithSummary("Add account chips")
			.WithDescription("Adds chips directly to the authenticated player's account balance and records an immutable ledger transaction.")
			.Produces<AddAccountChipsResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		return group;
	}
}
