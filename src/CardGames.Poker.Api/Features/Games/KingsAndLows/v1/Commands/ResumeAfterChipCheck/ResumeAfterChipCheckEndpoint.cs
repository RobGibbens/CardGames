using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.ResumeAfterChipCheck;

public static class ResumeAfterChipCheckEndpoint
{
	public static RouteGroupBuilder MapResumeAfterChipCheck(this RouteGroupBuilder group)
	{
		group.MapPost("/{gameId:guid}/resume-chip-check",
				async (Guid gameId, ResumeAfterChipCheckRequest? request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new ResumeAfterChipCheckCommand(gameId, request?.ForceResume ?? false);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => Results.Problem(
							title: "Resume after chip check error",
							detail: error.Message,
							statusCode: error.Code == ResumeAfterChipCheckErrorCode.GameNotFound
								? StatusCodes.Status404NotFound
								: StatusCodes.Status400BadRequest)
					);
				})
			.WithName($"KingsAndLows{nameof(MapResumeAfterChipCheck).TrimPrefix("Map")}")
			.WithSummary("Resume game after chip check pause")
			.WithDescription("Resume the game after the chip check pause. Players who still cannot cover the pot will auto-drop.")
			.Produces<ResumeAfterChipCheckSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}

/// <summary>
/// Request body for resume after chip check.
/// </summary>
/// <param name="ForceResume">If true, resume immediately even if some players are short (they will auto-drop).</param>
public record ResumeAfterChipCheckRequest(bool ForceResume = false);
