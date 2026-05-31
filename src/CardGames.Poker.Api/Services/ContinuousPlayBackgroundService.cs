using System.Diagnostics;
using System.Text.Json;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Infrastructure.Telemetry;
using CardGames.Poker.Betting;
using CardGames.Contracts.SignalR;
using Microsoft.EntityFrameworkCore;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Background service that monitors games for continuous play and automatically
/// starts new hands after the results display period expires.
/// </summary>
public sealed partial class ContinuousPlayBackgroundService : BackgroundService
{
	/// <summary>
	/// Duration in seconds for the results display period before starting the next hand.
	/// </summary>
	public const int ResultsDisplayDurationSeconds = 15;

	/// <summary>
	/// Duration in seconds for the draw complete display period before transitioning to showdown.
	/// This gives all players time to see their new cards.
	/// </summary>
	public const int DrawCompleteDisplayDurationSeconds = 5;

	/// <summary>
	/// Duration in seconds for the Klondike reveal display period before transitioning to showdown.
	/// This gives all players time to see the revealed wild card.
	/// </summary>
	public const int KlondikeRevealDisplayDurationSeconds = 20;

	/// <summary>
	/// Duration in seconds for the In-Between card reveal display period before advancing
	/// to the next player. This gives all players time to see the revealed third card.
	/// </summary>
	public const int InBetweenRevealDisplayDurationSeconds = 5;
	public const int CashRebuyGraceDurationSeconds = 20;
	public const int CashRebuyGameOverDisplayDurationSeconds = 15;

	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<ContinuousPlayBackgroundService> _logger;
	private readonly ContinuousPlayTelemetry? _telemetry;
	private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1);

	private const string PhaseNextHand = "next_hand";
	private const string PhaseDrawTransition = "draw_transition";
	private const string PhaseRebuyGrace = "rebuy_grace";
	private const string PhaseAbandoned = "abandoned";
	private const string PhaseLeagueSync = "league_sync";

	private const string OutcomeAdvanced = "advanced";
	private const string OutcomeSkipped = "skipped";
	private const string OutcomeFailed = "failed";

	/// <summary>
	/// Initializes a new instance of the <see cref="ContinuousPlayBackgroundService"/> class.
	/// </summary>
	public ContinuousPlayBackgroundService(
		IServiceScopeFactory scopeFactory,
		ILogger<ContinuousPlayBackgroundService> logger,
		ContinuousPlayTelemetry? telemetry = null)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
		_telemetry = telemetry;
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("ContinuousPlayBackgroundService started");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessGamesReadyForNextHandAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Expected during shutdown
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing continuous play games");
			}

			await Task.Delay(_checkInterval, stoppingToken);
		}

		_logger.LogInformation("ContinuousPlayBackgroundService stopped");
	}

	internal async Task ProcessGamesReadyForNextHandAsync(CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();
		try
		{
		using var scope = _scopeFactory.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var broadcaster = scope.ServiceProvider.GetRequiredService<IGameStateBroadcaster>();
		var leagueCompletionSync = scope.ServiceProvider.GetService<LeagueGameCompletionSyncService>();
		var handHistoryRecorder = scope.ServiceProvider.GetRequiredService<IHandHistoryRecorder>();
		var playerChipWalletService = scope.ServiceProvider.GetRequiredService<IPlayerChipWalletService>();
		var actionTimerService = scope.ServiceProvider.GetService(typeof(IActionTimerService)) as IActionTimerService;

		var now = DateTimeOffset.UtcNow;

		// Check for abandoned games (all players left after game started)
		await ProcessAbandonedGamesAsync(context, broadcaster, leagueCompletionSync, now, cancellationToken);

		// Process DrawComplete games that are ready to transition to Showdown
		await ProcessDrawCompleteGamesAsync(context, broadcaster, handHistoryRecorder, now, cancellationToken);

		// Process KlondikeReveal games that are ready to transition to Showdown
		await ProcessKlondikeRevealGamesAsync(context, broadcaster, now, cancellationToken);

		// Process In-Between games where the card reveal display period has expired
		await ProcessInBetweenResolutionGamesAsync(context, broadcaster, handHistoryRecorder, now, cancellationToken);

		// Find games in Complete or WaitingForPlayers phase where the next hand should start.
		// Also include Dealer's Choice games in WaitingToStart (after dealer chose the game type).
		var gamesReadyForNextHand = await context.Games
			.Where(g => (g.CurrentPhase == nameof(Phases.Complete)
						 || g.CurrentPhase == nameof(Phases.WaitingForPlayers)
						 || (g.CurrentPhase == nameof(Phases.WaitingToStart) && g.IsDealersChoice)) &&
						g.NextHandStartsAt != null &&
						g.NextHandStartsAt <= now &&
						(g.Status == GameStatus.InProgress || g.Status == GameStatus.BetweenHands))
			.Include(g => g.GamePlayers)
			.Include(g => g.GameType)
			.ToListAsync(cancellationToken);

	foreach (var game in gamesReadyForNextHand)
		{
			using var activity = StartContinuousPlayActivity(game, PhaseNextHand);
			try
			{
				var outcome = await StartNextHandAsync(scope, context, broadcaster, playerChipWalletService, actionTimerService, game, now, cancellationToken);
				RecordGameProcessed(PhaseNextHand, outcome);
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				RecordGameProcessed(PhaseNextHand, OutcomeFailed);
				_logger.LogError(ex, "Failed to start next hand for game {GameId}", game.Id);
			}
		}
	}
	finally
	{
		stopwatch.Stop();
		_telemetry?.RecordIteration(stopwatch.Elapsed.TotalMilliseconds);
	}
}

private void RecordGameProcessed(string phase, string outcome)
	=> _telemetry?.RecordGameProcessed(phase, outcome);

private static Activity? StartContinuousPlayActivity(Game game, string phase)
	=> StartContinuousPlayActivity(game.Id, game.CurrentHandNumber, phase);

private static Activity? StartContinuousPlayActivity(Guid gameId, int? handNumber, string phase)
{
	var activity = PokerActivitySource.Source.StartActivity("continuous_play.advance");
	activity?.SetTag("game.id", gameId);
	if (handNumber.HasValue)
	{
		activity?.SetTag("hand.number", handNumber.Value);
	}
	activity?.SetTag("phase", phase);
	return activity;
}
}
