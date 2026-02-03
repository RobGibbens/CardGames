using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Performs automatic actions when a player's turn timer expires.
/// </summary>
public sealed class AutoActionService : IAutoActionService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<AutoActionService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AutoActionService"/> class.
	/// </summary>
	public AutoActionService(
		IServiceScopeFactory scopeFactory,
		ILogger<AutoActionService> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task PerformAutoActionAsync(Guid gameId, int playerSeatIndex, CancellationToken cancellationToken = default)
	{
		_logger.LogInformation(
			"Performing auto-action for game {GameId}, player seat {SeatIndex}",
			gameId, playerSeatIndex);

		// Create a new scope for this operation since we're called from a timer callback
		using var scope = _scopeFactory.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
		var handlerFactory = scope.ServiceProvider.GetRequiredService<IGameFlowHandlerFactory>();

		// Get the game to determine the current phase and game type
		var game = await context.Games
			.Include(g => g.GameType)
			.Include(g => g.GamePlayers)
			.AsNoTracking()
			.FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

		if (game is null)
		{
			_logger.LogWarning("Game {GameId} not found for auto-action", gameId);
			return;
		}

		var currentPhase = game.CurrentPhase;
		var gameTypeCode = game.GameType?.Code;

		var handler = handlerFactory.GetHandler(gameTypeCode);

		_logger.LogDebug(
			"Auto-action context: GameId={GameId}, Phase={Phase}, GameType={GameType}",
			gameId, currentPhase, gameTypeCode);

		var actionContext = new AutoActionContext(
			gameId,
			playerSeatIndex,
			currentPhase,
			game,
			context,
			mediator,
			_logger,
			cancellationToken
		);

		await handler.PerformAutoActionAsync(actionContext);
	}
}
