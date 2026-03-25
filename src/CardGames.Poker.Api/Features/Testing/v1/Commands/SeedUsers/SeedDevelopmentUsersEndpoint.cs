using MediatR;

namespace CardGames.Poker.Api.Features.Testing.v1.Commands.SeedUsers;

public static class SeedDevelopmentUsersEndpoint
{
	public static RouteGroupBuilder MapSeedDevelopmentUsers(this RouteGroupBuilder group)
	{
		group.MapPost("users/seed",
				async (IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new SeedDevelopmentUsersCommand(), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							SeedDevelopmentUsersErrorCode.NoUsersConfigured =>
								Results.Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("SeedDevelopmentUsers")
			.WithSummary("Seed development login users")
			.WithDescription("Creates the configured development test users, marks them as confirmed, and skips any account that already exists.")
			.Produces<SeedDevelopmentUsersResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.AllowAnonymous();

		return group;
	}
}