using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using Microsoft.EntityFrameworkCore;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Background service that monitors games for continuous play and automatically
/// starts new hands after the results display period expires.
/// </summary>
public sealed class ContinuousPlayBackgroundService : BackgroundService
{
	/// <summary>
	/// Duration in seconds for the results display period before starting the next hand.
	/// </summary>
	public const int ResultsDisplayDurationSeconds = 8;

	/// <summary>
	/// Duration in seconds for the draw complete display period before transitioning to showdown.
	/// This gives all players time to see their new cards.
	/// </summary>
	public const int DrawCompleteDisplayDurationSeconds = 5;

	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<ContinuousPlayBackgroundService> _logger;
	private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Initializes a new instance of the <see cref="ContinuousPlayBackgroundService"/> class.
	/// </summary>
	public ContinuousPlayBackgroundService(
		IServiceScopeFactory scopeFactory,
		ILogger<ContinuousPlayBackgroundService> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
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

	private async Task ProcessGamesReadyForNextHandAsync(CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var broadcaster = scope.ServiceProvider.GetRequiredService<IGameStateBroadcaster>();
		var handHistoryRecorder = scope.ServiceProvider.GetRequiredService<IHandHistoryRecorder>();

		var now = DateTimeOffset.UtcNow;

		// Check for abandoned games (all players left after game started)
		await ProcessAbandonedGamesAsync(context, broadcaster, now, cancellationToken);

		// Process DrawComplete games that are ready to transition to Showdown
		await ProcessDrawCompleteGamesAsync(context, broadcaster, handHistoryRecorder, now, cancellationToken);

		// Find games in Complete or WaitingForPlayers phase where the next hand should start
		var gamesReadyForNextHand = await context.Games
			.Where(g => (g.CurrentPhase == nameof(Phases.Complete) || g.CurrentPhase == nameof(Phases.WaitingForPlayers)) &&
						g.NextHandStartsAt != null &&
						g.NextHandStartsAt <= now &&
						(g.Status == GameStatus.InProgress || g.Status == GameStatus.BetweenHands))
			.Include(g => g.GamePlayers)
			.Include(g => g.GameType)
			.ToListAsync(cancellationToken);

		foreach (var game in gamesReadyForNextHand)
			{
				try
				{
					await StartNextHandAsync(scope, context, broadcaster, game, now, cancellationToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to start next hand for game {GameId}", game.Id);
				}
			}
		}

	/// <summary>
	/// Checks for games where all players have left after the game started,
	/// and marks them as complete.
	/// </summary>
	private async Task ProcessAbandonedGamesAsync(
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// Find in-progress games (not waiting phases)
		var inProgressPhases = new[]
		{
					nameof(Phases.CollectingAntes),
					nameof(Phases.Dealing),
					nameof(Phases.FirstBettingRound),
					nameof(Phases.DrawPhase),
					nameof(Phases.SecondBettingRound),
					nameof(Phases.Showdown),
					nameof(Phases.Complete)
				};

		var activeGames = await context.Games
			.Where(g => inProgressPhases.Contains(g.CurrentPhase) &&
						(g.Status == GameStatus.InProgress || g.Status == GameStatus.BetweenHands))
			.Include(g => g.GamePlayers)
			.ToListAsync(cancellationToken);

		foreach (var game in activeGames)
		{
			// Check if all players have left (no active, connected players remaining)
			var activePlayers = game.GamePlayers
				.Where(gp => gp.Status == GamePlayerStatus.Active &&
							 gp.LeftAtHandNumber == -1)
				.ToList();

			if (activePlayers.Count == 0)
			{
				_logger.LogInformation(
					"Game {GameId} has no remaining players, marking as complete",
					game.Id);

				game.CurrentPhase = nameof(Phases.Complete);
				game.Status = GameStatus.Completed;
				game.EndedAt = now;
				game.UpdatedAt = now;
				game.NextHandStartsAt = null;
				game.HandCompletedAt = null;

				await context.SaveChangesAsync(cancellationToken);
				await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
			}
		}
	}

	/// <summary>
	/// Processes Kings and Lows games in DrawComplete phase that are ready to transition to Showdown.
	/// This allows players to see their new cards for a few seconds before showdown begins.
	/// </summary>
	private async Task ProcessDrawCompleteGamesAsync(
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		IHandHistoryRecorder handHistoryRecorder,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var drawCompleteDeadline = now.AddSeconds(-DrawCompleteDisplayDurationSeconds);

		// Find games in DrawComplete phase where the display period has expired
		var gamesReadyForShowdown = await context.Games
			.Where(g => g.CurrentPhase == nameof(Phases.DrawComplete) &&
						g.DrawCompletedAt != null &&
						g.DrawCompletedAt <= drawCompleteDeadline &&
						g.Status == GameStatus.InProgress)
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameCards)
			.Include(g => g.GameType)
			.ToListAsync(cancellationToken);

		using var scope = _scopeFactory.CreateScope();
		var flowHandlerFactory = scope.ServiceProvider.GetRequiredService<IGameFlowHandlerFactory>();

		foreach (var game in gamesReadyForShowdown)
		{
			try
			{
				_logger.LogInformation(
					"Game {GameId} DrawComplete display period expired, transitioning to Showdown",
					game.Id);

				// Use flow handler instead of hardcoded game type check
				var flowHandler = flowHandlerFactory.GetHandler(game.GameType?.Code);
				var nextPhase = await flowHandler.ProcessDrawCompleteAsync(
					context, game, handHistoryRecorder, now, cancellationToken);

				game.CurrentPhase = nextPhase;
				game.UpdatedAt = now;

				// If transitioning to Showdown and handler supports inline showdown
				if (nextPhase == nameof(Phases.Showdown) && flowHandler.SupportsInlineShowdown)
				{
					var showdownResult = await flowHandler.PerformShowdownAsync(
						context, game, handHistoryRecorder, now, cancellationToken);

					if (showdownResult.IsSuccess)
					{
						var postShowdownPhase = await flowHandler.ProcessPostShowdownAsync(
							context, game, showdownResult, now, cancellationToken);

						game.CurrentPhase = postShowdownPhase;
					}
				}

				await context.SaveChangesAsync(cancellationToken);
				await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to process DrawComplete for game {GameId}", game.Id);
			}
		}
	}

	/// <summary>
	/// Moves the dealer button to the next occupied seat position (clockwise).
	/// </summary>
	private static void MoveDealer(Game game)
	{
		var occupiedSeats = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.Select(gp => gp.SeatPosition)
			.ToList();

		if (occupiedSeats.Count == 0)
		{
			return;
		}

		var currentPosition = game.DealerPosition;
		var seatsAfterCurrent = occupiedSeats.Where(pos => pos > currentPosition).ToList();

		game.DealerPosition = seatsAfterCurrent.Count > 0
			? seatsAfterCurrent.First()
			: occupiedSeats.First();
	}

	private async Task StartNextHandAsync(
		IServiceScope scope,
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		Game game,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// 1. Finalize leave requests for players who were waiting for the hand to finish
		var playersLeaving = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.LeftAtHandNumber != -1)
			.ToList();

		foreach (var player in playersLeaving)
		{
			player.Status = GamePlayerStatus.Left;
			player.LeftAt = now;
			player.FinalChipCount = player.ChipStack;
			player.IsSittingOut = true;
			_logger.LogInformation(
				"Player {PlayerName} finalized leave from game {GameId} after hand {HandNumber}",
				player.Player?.Name ?? player.PlayerId.ToString(),
				game.Id,
				game.CurrentHandNumber);
		}

		if (playersLeaving.Count > 0)
		{
			await context.SaveChangesAsync(cancellationToken);
		}

		// Get the game flow handler for this game type (needed for game-specific checks below)
		var flowHandlerFactory = scope.ServiceProvider.GetRequiredService<IGameFlowHandlerFactory>();
		var flowHandler = flowHandlerFactory.GetHandler(game.GameType?.Code);

		// 2. Apply pending chips to player stacks
		var playersWithPendingChips = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.PendingChipsToAdd > 0)
			.ToList();

		foreach (var player in playersWithPendingChips)
		{
			player.ChipStack += player.PendingChipsToAdd;
			_logger.LogInformation(
				"Applied {PendingChips} pending chips to player {PlayerName} in game {GameId} (new stack: {NewStack})",
				player.PendingChipsToAdd,
				player.Player?.Name ?? player.PlayerId.ToString(),
				game.Id,
				player.ChipStack);
			player.PendingChipsToAdd = 0;
		}

		if (playersWithPendingChips.Count > 0)
		{
			await context.SaveChangesAsync(cancellationToken);
		}

		// 3. Check for eligible players (occupied, not sitting out, chips >= ante, hasn't left)
		var ante = game.Ante ?? 0;
		var eligiblePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active &&
						 !gp.IsSittingOut &&
						 gp.ChipStack >= ante &&
						 gp.LeftAtHandNumber == -1)
			.ToList();

		// Auto-sit-out players with insufficient chips (including 0 chips)
		var insufficientChipPlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active &&
						 !gp.IsSittingOut &&
						 (gp.ChipStack <= 0 || (ante > 0 && gp.ChipStack < ante)) &&
						 gp.LeftAtHandNumber == -1)
			.ToList();

		foreach (var player in insufficientChipPlayers)
			{
				player.IsSittingOut = true;
				_logger.LogInformation(
					"Player {PlayerName} auto-sat-out due to insufficient chips ({Chips} < {Ante}) in game {GameId}",
					player.Player?.Name ?? player.PlayerId.ToString(),
					player.ChipStack,
					ante,
					game.Id);
			}

			// 3a. For games with chip coverage check requirement, check if any player cannot cover the current pot
			if (flowHandler.RequiresChipCoverageCheck)
			{
				var chipCheckConfig = flowHandler.GetChipCheckConfiguration();
				if (chipCheckConfig.IsEnabled)
				{
					// Calculate the pot amount for the upcoming hand
					var currentPotAmount = await context.Pots
						.Where(p => p.GameId == game.Id && !p.IsAwarded)
						.SumAsync(p => p.Amount, cancellationToken);

					_logger.LogInformation(
						"[CHIP-CHECK-BG] Game {GameId}: Checking chip coverage. CurrentPot={PotAmount}, EligiblePlayers={PlayerCount}",
						game.Id, currentPotAmount, eligiblePlayers.Count);

					// Check if any eligible player cannot cover the pot
					var playersNeedingChips = eligiblePlayers
						.Where(p => p.ChipStack < currentPotAmount && !p.AutoDropOnDropOrStay)
						.ToList();

					if (currentPotAmount > 0 && playersNeedingChips.Count > 0)
					{
						// If game is already paused for chip check, check if timer expired
						if (game.IsPausedForChipCheck)
						{
							if (game.ChipCheckPauseEndsAt.HasValue && now >= game.ChipCheckPauseEndsAt.Value)
							{
								// Timer expired - apply shortage action
								foreach (var shortPlayer in playersNeedingChips)
								{
									if (chipCheckConfig.ShortageAction == GameFlow.ChipShortageAction.AutoDrop)
									{
										shortPlayer.AutoDropOnDropOrStay = true;
									}
									else if (chipCheckConfig.ShortageAction == GameFlow.ChipShortageAction.SitOut)
									{
										shortPlayer.IsSittingOut = true;
									}
									_logger.LogInformation(
										"[CHIP-CHECK-BG] Chip check pause expired: Player {PlayerName} at seat {Seat} action applied (chips: {ChipStack}, pot: {PotAmount})",
										shortPlayer.Player?.Name ?? "Unknown", shortPlayer.SeatPosition, shortPlayer.ChipStack, currentPotAmount);
								}

								// Clear pause state and continue with hand
								game.IsPausedForChipCheck = false;
								game.ChipCheckPauseStartedAt = null;
								game.ChipCheckPauseEndsAt = null;
								game.UpdatedAt = now;
								await context.SaveChangesAsync(cancellationToken);
							}
							else
							{
								// Still within pause period - broadcast state and wait
								_logger.LogInformation(
									"[CHIP-CHECK-BG] Game {GameId} still paused for chip check. {Count} player(s) need chips. Ends at {EndTime}",
									game.Id, playersNeedingChips.Count, game.ChipCheckPauseEndsAt);
								await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
								return; // Don't start the hand yet
							}
						}
						else
						{
							// New chip shortage detected - initiate pause
							game.IsPausedForChipCheck = true;
							game.ChipCheckPauseStartedAt = now;
							game.ChipCheckPauseEndsAt = now.Add(chipCheckConfig.PauseDuration);
							game.UpdatedAt = now;

							var shortPlayerNames = string.Join(", ", playersNeedingChips.Select(p => $"{p.Player?.Name ?? "Unknown"}({p.ChipStack})"));
							_logger.LogWarning(
								"[CHIP-CHECK-BG] Game {GameId}: Pausing for chip check. {PlayerCount} player(s) cannot cover pot of {PotAmount}. Players: {PlayerNames}",
								game.Id, playersNeedingChips.Count, currentPotAmount, shortPlayerNames);

							await context.SaveChangesAsync(cancellationToken);
							await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
							return; // Don't start the hand - wait for players to add chips
						}
					}
					else if (game.IsPausedForChipCheck)
					{
						// All players now have enough chips - clear pause
						game.IsPausedForChipCheck = false;
						game.ChipCheckPauseStartedAt = null;
						game.ChipCheckPauseEndsAt = null;
						game.UpdatedAt = now;
						_logger.LogInformation("[CHIP-CHECK-BG] Game {GameId}: All players now have sufficient chips, resuming", game.Id);
					}
				}
			}

			// Check if all players have left - end the game
		var remainingPlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.LeftAtHandNumber == -1)
			.ToList();

		if (remainingPlayers.Count == 0)
		{
			_logger.LogInformation(
				"Game {GameId} has no remaining players, ending game",
				game.Id);

			game.CurrentPhase = nameof(Phases.Complete);
			game.Status = GameStatus.Completed;
			game.EndedAt = now;
			game.NextHandStartsAt = null;
			game.HandCompletedAt = null;
			game.UpdatedAt = now;

			await context.SaveChangesAsync(cancellationToken);
			await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
			return;
		}

		// Check minimum player count
		if (eligiblePlayers.Count < 2)
		{
			_logger.LogInformation(
				"Game {GameId} has insufficient eligible players ({Count}), pausing continuous play",
				game.Id,
				eligiblePlayers.Count);

			// Clear cards from previous hand when transitioning to WaitingForPlayers
			// This prevents the previous hand's cards from being displayed while waiting
			var existingCardsToRemove = await context.GameCards
				.Where(gc => gc.GameId == game.Id)
				.ToListAsync(cancellationToken);

			if (existingCardsToRemove.Count > 0)
			{
				context.GameCards.RemoveRange(existingCardsToRemove);
			}

			// Reset player states (clear folded/all-in flags from previous hand)
			foreach (var gamePlayer in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active))
			{
				gamePlayer.CurrentBet = 0;
				gamePlayer.TotalContributedThisHand = 0;
				gamePlayer.HasFolded = false;
				gamePlayer.IsAllIn = false;
				gamePlayer.HasDrawnThisRound = false;
				gamePlayer.DropOrStayDecision = null;
			}

			// Pause continuous play - wait for players
			game.CurrentPhase = nameof(Phases.WaitingForPlayers);
			game.NextHandStartsAt = null;
			game.Status = GameStatus.BetweenHands;
			game.UpdatedAt = now;

			await context.SaveChangesAsync(cancellationToken);
			await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
			return;
		}

		// Reset player states for new hand
		foreach (var gamePlayer in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active))
		{
			gamePlayer.CurrentBet = 0;
			gamePlayer.TotalContributedThisHand = 0;
			// Players sitting out are treated as folded for this hand
			gamePlayer.HasFolded = gamePlayer.IsSittingOut;
			gamePlayer.IsAllIn = false;
			gamePlayer.HasDrawnThisRound = false;
			gamePlayer.DropOrStayDecision = null;
		}

		// Remove any existing cards from previous hand
		var existingCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id)
			.ToListAsync(cancellationToken);

		if (existingCards.Count > 0)
		{
			context.GameCards.RemoveRange(existingCards);
		}

		// Mark any incomplete betting rounds from the previous hand as complete
		var incompleteBettingRounds = await context.Set<Data.Entities.BettingRound>()
			.Where(br => br.GameId == game.Id && !br.IsComplete)
			.ToListAsync(cancellationToken);

		foreach (var br in incompleteBettingRounds)
		{
			br.IsComplete = true;
			br.CompletedAt = now;
		}

		// Find unawarded pots from the current hand (e.g., if all players dropped) and carry them over
		var previousHandPots = await context.Pots
			.Where(p => p.GameId == game.Id && p.HandNumber == game.CurrentHandNumber && !p.IsAwarded)
			.ToListAsync(cancellationToken);

		var carriedOverAmount = 0;
		if (previousHandPots.Count > 0)
		{
			carriedOverAmount = previousHandPots.Sum(p => p.Amount);
			foreach (var pot in previousHandPots)
			{
				pot.IsAwarded = true;
				pot.AwardedAt = now;
				pot.WinReason = "Carried over - all players dropped";
			}

			_logger.LogInformation(
				"Carrying over {Amount} chips from unawarded pots in game {GameId} hand {HandNumber}",
				carriedOverAmount, game.Id, game.CurrentHandNumber);
		}

		// Check if a main pot already exists for the next hand (e.g., from pot matching in Kings and Lows)
		var existingPot = await context.Pots
			.FirstOrDefaultAsync(p => p.GameId == game.Id &&
									  p.HandNumber == game.CurrentHandNumber + 1 &&
									  p.PotType == PotType.Main,
								 cancellationToken);

		if (existingPot is null)
		{
			// Create a new main pot for this hand (with any carried-over amount)
			var mainPot = new Pot
			{
				GameId = game.Id,
				HandNumber = game.CurrentHandNumber + 1,
				PotType = PotType.Main,
				PotOrder = 0,
				Amount = carriedOverAmount,
				IsAwarded = false,
				CreatedAt = now
			};

			context.Pots.Add(mainPot);
		}
		else if (carriedOverAmount > 0)
		{
			// Add carried-over amount to existing pot
			existingPot.Amount += carriedOverAmount;
		}

		// NOTE: Dealer rotation is already done in PerformShowdownCommandHandler.MoveDealer()
		// when the previous hand completes. We do NOT rotate again here.

		// Get dealing configuration from the flow handler
		var dealingConfig = flowHandler.GetDealingConfiguration();

		// Update game state
		game.CurrentHandNumber++;
		// Use flow handler to determine the initial phase for this game type
		game.CurrentPhase = flowHandler.GetInitialPhase(game);
		game.Status = GameStatus.InProgress;
		game.CurrentPlayerIndex = -1;
		game.CurrentDrawPlayerIndex = -1;
		game.HandCompletedAt = null;
		game.NextHandStartsAt = null;
		game.UpdatedAt = now;

		// Perform game-specific initialization
		await flowHandler.OnHandStartingAsync(game, cancellationToken);

		await context.SaveChangesAsync(cancellationToken);

		_logger.LogInformation(
			"Started hand {HandNumber} for game {GameId} ({GameType}) with {PlayerCount} eligible players. Dealer at seat {DealerPosition}. Initial phase: {InitialPhase}",
			game.CurrentHandNumber,
			game.Id,
			flowHandler.GameTypeCode,
			eligiblePlayers.Count,
			game.DealerPosition,
			game.CurrentPhase);

		// Automatically collect antes (skip if game's flow handler indicates it handles antes differently)
		if (!flowHandler.SkipsAnteCollection)
		{
			await CollectAntesAsync(context, game, eligiblePlayers, ante, now, cancellationToken);
		}

		// Automatically deal hands using the flow handler
		await flowHandler.DealCardsAsync(context, game, eligiblePlayers, now, cancellationToken);

		// Broadcast updated state
		await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
	}

	private async Task CollectAntesAsync(
		CardsDbContext context,
		Game game,
		List<GamePlayer> eligiblePlayers,
		int ante,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (ante <= 0)
		{
			game.CurrentPhase = nameof(Phases.Dealing);
			return;
		}

		var pot = await context.Pots
			.FirstOrDefaultAsync(p => p.GameId == game.Id &&
									  p.HandNumber == game.CurrentHandNumber &&
									  p.PotType == PotType.Main,
							 cancellationToken);

		foreach (var player in eligiblePlayers)
		{
			var anteAmount = Math.Min(ante, player.ChipStack);
			player.ChipStack -= anteAmount;
			player.CurrentBet = anteAmount;
			player.TotalContributedThisHand = anteAmount;

			if (pot is not null)
			{
				pot.Amount += anteAmount;

				var contribution = new PotContribution
				{
					PotId = pot.Id,
					GamePlayerId = player.Id,
					Amount = anteAmount,
					ContributedAt = now
				};
				context.Set<PotContribution>().Add(contribution);
			}

			if (player.ChipStack == 0)
			{
				player.IsAllIn = true;
			}
		}

		game.CurrentPhase = nameof(Phases.Dealing);
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);
	}

	/// <summary>
	/// Finds the first active player after the dealer position who can act.
	/// </summary>
	private static int FindFirstActivePlayerAfterDealer(Game game, List<GamePlayer> activePlayers)
	{
		if (activePlayers.Count == 0)
		{
			return -1;
		}

		var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
		var totalSeats = maxSeatPosition + 1;
		var searchIndex = (game.DealerPosition + 1) % totalSeats;

		// Search through all seat positions starting left of dealer
		for (var i = 0; i < totalSeats; i++)
		{
			var player = activePlayers.FirstOrDefault(p => p.SeatPosition == searchIndex);
			if (player is not null && !player.HasFolded && !player.IsAllIn)
			{
				return searchIndex;
			}
			searchIndex = (searchIndex + 1) % totalSeats;
		}

		return -1; // No active player found
	}
}
