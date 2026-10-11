using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Infrastructure.PipelineBehaviors;

/// <summary>
///     Binds HTTP game mutations to the authenticated caller.
///     In-process calls with no <see cref="HttpContext" /> (action timers, continuous play, mediator tests) pass through.
/// </summary>
public sealed class GameAuthorizationBehavior<TRequest, TResponse>(
	IHttpContextAccessor httpContextAccessor,
	ICurrentUserService currentUserService,
	IGamePlayerResolver gamePlayerResolver,
	CardsDbContext context)
	: IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
{
	public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken)
	{
		if (httpContextAccessor.HttpContext is null || request is not IRequiresAuthentication)
			return await next(cancellationToken);

		if (!currentUserService.IsAuthenticated)
			throw new GameAuthorizationException(StatusCodes.Status401Unauthorized, "NotAuthenticated",
				"Authentication is required.");

		if (request is not IGameStateChangingCommand gameCommand)
			return await next(cancellationToken);

		if (request is IRequiresCurrentActor)
			await EnsureCurrentActorAsync(gameCommand.GameId, cancellationToken);
		else if (request is IRequiresSeatedPlayer)
			await EnsureSeatedAsync(gameCommand.GameId, cancellationToken);
		else if (request is IRequiresHostOrSeatedPlayer)
			await EnsureHostOrSeatedAsync(gameCommand.GameId, cancellationToken);

		return await next(cancellationToken);
	}

	private async Task EnsureCurrentActorAsync(Guid gameId, CancellationToken cancellationToken)
	{
		if (!await GameExistsAsync(gameId, cancellationToken))
			return;

		var seated = await gamePlayerResolver.ResolveSeatedPlayerAsync(gameId, cancellationToken);
		if (seated is null)
			throw Forbidden("Not seated at this table.");

		var actorSeat = await ResolveActorSeatAsync(gameId, cancellationToken);
		if (actorSeat is null)
			return;

		if (seated.SeatPosition != actorSeat.Value)
			throw new GameAuthorizationException(StatusCodes.Status403Forbidden, "NotPlayerTurn",
				"It is not your turn.");
	}

	private async Task EnsureSeatedAsync(Guid gameId, CancellationToken cancellationToken)
	{
		if (!await GameExistsAsync(gameId, cancellationToken))
			return;

		var seated = await gamePlayerResolver.ResolveSeatedPlayerAsync(gameId, cancellationToken);
		if (seated is null)
			throw Forbidden("Not seated at this table.");
	}

	private async Task EnsureHostOrSeatedAsync(Guid gameId, CancellationToken cancellationToken)
	{
		if (!await GameExistsAsync(gameId, cancellationToken))
			return;

		var seated = await gamePlayerResolver.ResolveSeatedPlayerAsync(gameId, cancellationToken);
		if (seated is not null)
			return;

		if (await gamePlayerResolver.IsTableHostAsync(gameId, cancellationToken))
			return;

		throw Forbidden("Only a seated player or the table host can do that.");
	}

	private async Task<int?> ResolveActorSeatAsync(Guid gameId, CancellationToken cancellationToken)
	{
		if (typeof(TRequest).Name.Contains("ProcessBettingAction", StringComparison.Ordinal))
		{
			var round = await context.BettingRounds
				.AsNoTracking()
				.Where(b => b.GameId == gameId && !b.IsComplete)
				.OrderByDescending(b => b.RoundNumber)
				.Select(b => (int?)b.CurrentActorIndex)
				.FirstOrDefaultAsync(cancellationToken);
			return round;
		}

		var drawIndex = await context.Games
			.AsNoTracking()
			.Where(g => g.Id == gameId)
			.Select(g => (int?)g.CurrentDrawPlayerIndex)
			.FirstOrDefaultAsync(cancellationToken);

		return drawIndex is null or < 0 ? null : drawIndex;
	}

	private Task<bool> GameExistsAsync(Guid gameId, CancellationToken cancellationToken) =>
		context.Games.AsNoTracking().AnyAsync(g => g.Id == gameId, cancellationToken);

	private static GameAuthorizationException Forbidden(string message) =>
		new(StatusCodes.Status403Forbidden, "NotAuthorized", message);
}
