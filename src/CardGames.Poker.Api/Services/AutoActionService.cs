using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.GameFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services;

/// <summary>
///     Performs automatic actions when a player's turn timer expires.
/// </summary>
public sealed class AutoActionService : IAutoActionService
{
	private readonly ILogger<AutoActionService> _logger;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly GameEventsTelemetry? _telemetry;

	/// <summary>
	///     Initializes a new instance of the <see cref="AutoActionService" /> class.
	/// </summary>
	public AutoActionService(
		IServiceScopeFactory scopeFactory,
		ILogger<AutoActionService> logger,
		GameEventsTelemetry? telemetry = null)
	{
		_scopeFactory = scopeFactory;
		_telemetry = telemetry;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task PerformAutoActionAsync(Guid gameId, int playerSeatIndex,
		CancellationToken cancellationToken = default)
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
		var target = await AutoActionSeatResolver.ResolveAsync(
			context, game, handler.GetGameRules(), cancellationToken);

		if (target.Kind == AutoActionTargetKind.None)
		{
			_logger.LogInformation(
				"Skipping auto-action for game {GameId}: phase {Phase} requires no actor",
				gameId, currentPhase);
			return;
		}

		if (target.Kind != AutoActionTargetKind.Simultaneous
		    && playerSeatIndex >= 0
		    && playerSeatIndex != target.SeatIndex)
		{
			_logger.LogWarning(
				"Skipping stale auto-action for game {GameId}: timer seat {TimerSeat} but current actor is {ActorSeat} (phase {Phase})",
				gameId, playerSeatIndex, target.SeatIndex, currentPhase);
			return;
		}

		if (target.UsedCurrentPlayerIndexFallback)
		{
			_logger.LogWarning(
				"No active betting round for game {GameId} in phase {Phase}; using CurrentPlayerIndex {ActorSeat}",
				gameId, currentPhase, target.SeatIndex);
		}

		var effectiveSeat = target.Kind == AutoActionTargetKind.Simultaneous ? -1 : target.SeatIndex;

		_logger.LogDebug(
			"Auto-action context: GameId={GameId}, Phase={Phase}, GameType={GameType}, Seat={SeatIndex}",
			gameId, currentPhase, gameTypeCode, effectiveSeat);

		_telemetry?.RecordPlayerEvent(GameEventsTelemetry.ResolveGameType(game),
			GameEventsTelemetry.ResolveTableKind(game),
			"timed_out");

		var actionContext = new AutoActionContext(
			gameId,
			effectiveSeat,
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