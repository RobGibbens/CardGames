using CardGames.Poker.Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleSitOut;

public record ToggleSitOutRequest(bool IsSittingOut);

public static class ToggleSitOutEndpoint
{
	public static RouteGroupBuilder MapSitOut(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/sit-out",
				async (Guid gameId, [FromBody] ToggleSitOutRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new ToggleSitOutCommand(gameId, request.IsSittingOut);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => Results.BadRequest(new { error.Message })); // Using BadRequest for simple errors
				})
			.WithName("SitOut")
			.WithSummary("Toggle Sit Out Status")
			.WithDescription("Updates the sitting out status of the current player for the specified game.");

		return group;
	}
}
