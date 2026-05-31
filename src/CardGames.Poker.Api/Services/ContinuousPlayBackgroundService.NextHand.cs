using System.Text.Json;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using CardGames.Contracts.SignalR;
using Microsoft.EntityFrameworkCore;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Services;

public sealed partial class ContinuousPlayBackgroundService
{
	/// <summary>
	/// Moves the dealer button to the next occupied seat position (clockwise).
	/// Prefers active non-sitting-out players to avoid placing the dealer on someone
	/// who isn't participating (which would break Player vs Deck decision-maker logic).
	/// </summary>
	private static void MoveDealer(Game game)
	{
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.IsSittingOut)
			.OrderBy(gp => gp.SeatPosition)
			.Select(gp => gp.SeatPosition)
			.ToList();

		// Fallback to all active players (including sitting out) if no active non-sitting-out players
		if (activePlayers.Count == 0)
		{
			activePlayers = game.GamePlayers
				.Where(gp => gp.Status == GamePlayerStatus.Active)
				.OrderBy(gp => gp.SeatPosition)
				.Select(gp => gp.SeatPosition)
				.ToList();
		}

		if (activePlayers.Count == 0)
		{
			return;
		}

		var currentPosition = game.DealerPosition;
		var seatsAfterCurrent = activePlayers.Where(pos => pos > currentPosition).ToList();

		game.DealerPosition = seatsAfterCurrent.Count > 0
			? seatsAfterCurrent.First()
			: activePlayers.First();
	}

	/// <summary>
	/// Advances the Dealer's Choice dealer position to the next active seat (clockwise).
	/// This is separate from the in-game dealer rotation (MoveDealer) because multi-hand games
	/// like Kings and Lows rotate the in-game dealer internally while the DC turn stays fixed.
	/// When <see cref="Game.OriginalDealersChoiceDealerPosition"/> is set, advance from that
	/// position (the player who originally chose the variant) so the deal rotates correctly
	/// even after a multi-hand variant like Kings and Lows rotated the DC dealer internally.
	/// </summary>
	private static void AdvanceDealersChoiceDealer(Game game)
	{
		var occupiedSeats = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.LeftAtHandNumber == -1)
			.OrderBy(gp => gp.SeatPosition)
			.Select(gp => gp.SeatPosition)
			.ToList();

		if (occupiedSeats.Count == 0)
		{
			return;
		}

		// Advance from the original picker's seat when a multi-hand variant ends,
		// otherwise advance from the current DC dealer seat.
		var advanceFrom = game.OriginalDealersChoiceDealerPosition
		                  ?? game.DealersChoiceDealerPosition
		                  ?? 0;

		var seatsAfterCurrent = occupiedSeats.Where(pos => pos > advanceFrom).ToList();

		game.DealersChoiceDealerPosition = seatsAfterCurrent.Count > 0
			? seatsAfterCurrent.First()
			: occupiedSeats.First();

		// Clear the saved original position now that the variant is over.
		game.OriginalDealersChoiceDealerPosition = null;
	}

	private async Task<string> StartNextHandAsync(
		IServiceScope scope,
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		IPlayerChipWalletService playerChipWalletService,
		IActionTimerService? actionTimerService,
		Game game,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// Re-check readiness against the latest database state to avoid races with manual StartHand.
		// ReloadAsync only refreshes scalar properties; explicitly reload the GameType navigation
		// so that non-Dealer's-Choice games still resolve the correct flow handler.
		await context.Entry(game).ReloadAsync(cancellationToken);
		await context.Entry(game).Reference(g => g.GameType).LoadAsync(cancellationToken);

		foreach (var gp in game.GamePlayers.ToList())
		{
			await context.Entry(gp).ReloadAsync(cancellationToken);
		}

		var stillReadyForNextHand =
			(game.CurrentPhase == nameof(Phases.Complete)
			 || game.CurrentPhase == nameof(Phases.WaitingForPlayers)
			 || (game.CurrentPhase == nameof(Phases.WaitingToStart) && game.IsDealersChoice)) &&
			game.NextHandStartsAt != null &&
			game.NextHandStartsAt <= now &&
			(game.Status == GameStatus.InProgress || game.Status == GameStatus.BetweenHands);

		if (!stillReadyForNextHand)
		{
			_logger.LogDebug(
				"Skipping StartNextHand for game {GameId}; game no longer ready (Phase={Phase}, Status={Status}, NextHandStartsAt={NextHandStartsAt})",
				game.Id,
				game.CurrentPhase,
				game.Status,
				game.NextHandStartsAt);
			return OutcomeSkipped;
		}

		// 1. Finalize leave requests for players who were waiting for the hand to finish
		var playersLeaving = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.LeftAtHandNumber != -1)
			.ToList();

		foreach (var player in playersLeaving)
		{
			await playerChipWalletService.CreditForCashOutAsync(
				player.PlayerId,
				player.ChipStack,
				game.Id,
				null,
				cancellationToken);

			player.Status = GamePlayerStatus.Left;
			player.LeftAt = now;
			player.FinalChipCount = player.ChipStack;
			// Zero ChipStack so the rejoin path knows these chips are already back in the wallet
			player.ChipStack = 0;
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
		// For Dealer's Choice, CurrentHandGameTypeCode is the chosen game for this hand.
		// For standard games, use GameType.Code (reliably loaded after ReloadAsync + Reference load above).
		var flowHandlerFactory = scope.ServiceProvider.GetRequiredService<IGameFlowHandlerFactory>();
		var flowHandler = flowHandlerFactory.GetHandler(game.CurrentHandGameTypeCode ?? game.GameType?.Code);
		var leagueCompletionSync = scope.ServiceProvider.GetService<LeagueGameCompletionSyncService>();

		// 2. Apply pending chips to player stacks (validate cashier balance first)
		var playersWithPendingChips = game.GamePlayers
    			.Where(gp =>
        				gp.Status is GamePlayerStatus.Active or GamePlayerStatus.SittingOut &&
        				gp.PendingChipsToAdd > 0)
    			.ToList();

		foreach (var player in playersWithPendingChips)
		{
			// Validate cashier balance can cover the pending amount
			var cashierBalance = await playerChipWalletService.GetBalanceAsync(player.PlayerId, cancellationToken);
			if (cashierBalance < player.PendingChipsToAdd)
			{
				_logger.LogWarning(
					"Insufficient cashier balance for pending chips: player {PlayerName} in game {GameId}. Balance: {Balance}, Pending: {Pending}. Cancelling pending chips.",
					player.Player?.Name ?? player.PlayerId.ToString(),
					game.Id,
					cashierBalance,
					player.PendingChipsToAdd);
				player.PendingChipsToAdd = 0;
				continue;
			}

			player.ChipStack += player.PendingChipsToAdd;
			player.BringInAmount += player.PendingChipsToAdd;
			_logger.LogInformation(
				"Applied {PendingChips} pending chips to player {PlayerName} in game {GameId} (new stack: {NewStack})",
				player.PendingChipsToAdd,
				player.Player?.Name ?? player.PlayerId.ToString(),
				game.Id,
				player.ChipStack);

			player.PendingChipsToAdd = 0;
			player.IsSittingOut = false;
			player.Status = GamePlayerStatus.Active;
		}

		if (playersWithPendingChips.Count > 0)
		{
			await context.SaveChangesAsync(cancellationToken);
		}

		// 3. Check for eligible players (occupied, not sitting out, chips >= ante, hasn't left)
		var ante = game.Ante ?? 0;
		var isTournamentGame = game.TournamentBuyIn.HasValue;
		var eligiblePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active &&
						 !gp.IsSittingOut &&
						 gp.ChipStack >= ante &&
						 gp.LeftAtHandNumber == -1)
			.ToList();

		var tournamentBustedPlayers = isTournamentGame
			? game.GamePlayers
				.Where(gp => gp.Status != GamePlayerStatus.Left &&
							 gp.LeftAtHandNumber == -1 &&
							 gp.ChipStack <= 0)
				.ToList()
			: new List<GamePlayer>();

		if (tournamentBustedPlayers.Count > 0)
		{
			MarkTournamentBustedPlayersAsObservers(tournamentBustedPlayers, now);
		}

		// Auto-sit-out players with insufficient chips (including 0 chips)
		var insufficientChipPlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active &&
						 !gp.IsSittingOut &&
						 (isTournamentGame
							 ? ante > 0 && gp.ChipStack > 0 && gp.ChipStack < ante
							 : gp.ChipStack <= 0 || (ante > 0 && gp.ChipStack < ante)) &&
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

		// Recompute eligible players after auto-sit-out so downstream checks
		// (e.g. TryTransitionTerminalDealersChoiceScrewYourNeighborAsync) use
		// the correct count. Without this, ante=0 games could include 0-chip
		// players who were just sat out, inflating the count.
		if (tournamentBustedPlayers.Count > 0 || insufficientChipPlayers.Count > 0)
		{
			eligiblePlayers = game.GamePlayers
				.Where(gp => gp.Status == GamePlayerStatus.Active &&
							 !gp.IsSittingOut &&
							 gp.ChipStack >= ante &&
							 gp.LeftAtHandNumber == -1)
				.ToList();
		}

		if (await TryCompleteTournamentWhenOneFundedPlayerRemainsAsync(
			context,
			broadcaster,
			leagueCompletionSync,
			game,
			now,
			cancellationToken))
		{
			return OutcomeSkipped;
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
								game.IsPausedForRebuyGrace = false;
								game.RebuyGraceStartedAt = null;
								game.RebuyGraceEndsAt = null;
								game.UpdatedAt = now;
								await context.SaveChangesAsync(cancellationToken);
							}
							else
							{
								// Still within pause period - broadcast state and wait
								_logger.LogInformation(
									"[CHIP-CHECK-BG] Game {GameId} still paused for chip check. {Count} player(s) need chips. Ends at {EndTime}",
									game.Id, playersNeedingChips.Count, game.ChipCheckPauseEndsAt);
							actionTimerService?.StartChipCheckPauseTimer(
								game.Id,
								durationSeconds: (int)Math.Ceiling(chipCheckConfig.PauseDuration.TotalSeconds),
								startedAtUtc: now);
							await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
							return OutcomeSkipped; // Don't start the hand yet
						}
						}
						else
						{
							// New chip shortage detected - initiate pause
							game.IsPausedForChipCheck = true;
							game.ChipCheckPauseStartedAt = now;
							game.ChipCheckPauseEndsAt = now.Add(chipCheckConfig.PauseDuration);
							game.IsPausedForRebuyGrace = false;
							game.RebuyGraceStartedAt = null;
							game.RebuyGraceEndsAt = null;
							game.UpdatedAt = now;

							var shortPlayerNames = string.Join(", ", playersNeedingChips.Select(p => $"{p.Player?.Name ?? "Unknown"}({p.ChipStack})"));
							_logger.LogWarning(
								"[CHIP-CHECK-BG] Game {GameId}: Pausing for chip check. {PlayerCount} player(s) cannot cover pot of {PotAmount}. Players: {PlayerNames}",
								game.Id, playersNeedingChips.Count, currentPotAmount, shortPlayerNames);

							await context.SaveChangesAsync(cancellationToken);
							await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
							return OutcomeSkipped; // Don't start the hand - wait for players to add chips
						}
					}
					else if (game.IsPausedForChipCheck)
					{
						// All players now have enough chips - clear pause
						game.IsPausedForChipCheck = false;
						game.ChipCheckPauseStartedAt = null;
						game.ChipCheckPauseEndsAt = null;
						game.IsPausedForRebuyGrace = false;
						game.RebuyGraceStartedAt = null;
						game.RebuyGraceEndsAt = null;
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
			game.IsPausedForChipCheck = false;
			game.ChipCheckPauseStartedAt = null;
			game.ChipCheckPauseEndsAt = null;
			game.IsPausedForRebuyGrace = false;
			game.RebuyGraceStartedAt = null;
			game.RebuyGraceEndsAt = null;
			game.UpdatedAt = now;

			await context.SaveChangesAsync(cancellationToken);
			await SyncLeagueCompletionIfNeededAsync(leagueCompletionSync, game.Id, cancellationToken);
			await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
			return OutcomeSkipped;
		}

		if (await TryTransitionTerminalDealersChoiceScrewYourNeighborAsync(
			context,
			broadcaster,
			game,
			flowHandler,
			eligiblePlayers.Count,
			now,
			cancellationToken))
		{
			return OutcomeSkipped;
		}

		if (await TryTransitionTerminalDealersChoiceInBetweenAsync(
			context,
			broadcaster,
			game,
			now,
			cancellationToken))
		{
			return OutcomeSkipped;
		}

		if (await TryHandleCashGameRebuyGraceAsync(
			context,
			broadcaster,
			leagueCompletionSync,
			actionTimerService,
			game,
			eligiblePlayers,
			ante,
			now,
			cancellationToken))
		{
			return OutcomeSkipped;
		}

		// Check minimum player count
		if (eligiblePlayers.Count < 2)
		{
			// Dealer's Choice tables should return to game selection, not WaitingForPlayers,
			// when a variant finishes with insufficient eligible players. The next dealer can
			// pick a different game type once more players join or add chips.
			if (game.IsDealersChoice &&
				game.CurrentPhase != nameof(Phases.WaitingToStart) &&
				!string.IsNullOrWhiteSpace(game.CurrentHandGameTypeCode))
			{
				_logger.LogInformation(
					"[DC-FALLBACK] Game {GameId}: Fewer than 2 eligible players after variant {GameType}, " +
					"returning to WaitingForDealerChoice instead of WaitingForPlayers (EligibleCount={EligibleCount})",
					game.Id, game.CurrentHandGameTypeCode, eligiblePlayers.Count);

				await TransitionDealersChoiceToWaitingForChoiceAsync(
					context,
					broadcaster,
					game,
					now,
					cancellationToken,
					"Dealer's Choice game {GameId}: variant ended with insufficient players, waiting for dealer at seat {DcDealerSeat} to choose next game");
				return OutcomeSkipped;
			}

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
			game.IsPausedForChipCheck = false;
			game.ChipCheckPauseStartedAt = null;
			game.ChipCheckPauseEndsAt = null;
			game.IsPausedForRebuyGrace = false;
			game.RebuyGraceStartedAt = null;
			game.RebuyGraceEndsAt = null;
			game.UpdatedAt = now;

			await context.SaveChangesAsync(cancellationToken);
			await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
			return OutcomeSkipped;
		}

		// SYN in DC stores pre-SYN chip snapshots in VariantState; preserve across multi-hand rounds.
		var preserveVariantState = game.IsDealersChoice &&
			string.Equals(flowHandler.GameTypeCode, PokerGameMetadataRegistry.ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase);

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
			if (!preserveVariantState)
				gamePlayer.VariantState = null;
		}

		// Prepare cards for the upcoming hand. Most games clear prior cards here,
		// but variants with persistent decks can retain undealt cards.
		await flowHandler.PrepareForNewHandAsync(
			context,
			game,
			eligiblePlayers,
			game.CurrentHandNumber + 1,
			now,
			cancellationToken);

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

		// Dealer's Choice: determine whether to continue the current game variant or prompt for a new choice.
		// A pre-existing pot for the next hand (created by multi-hand game flow, e.g. Kings and Lows pot matching)
		// signals the current variant wants another hand. Otherwise, rotate the DC dealer and wait for a new choice.
		// SKIP this rotation when the game is in WaitingToStart — the dealer just chose the game type and we
		// need to deal the first hand of this variant, not rotate to the next dealer.
		if (game.IsDealersChoice && game.CurrentPhase != nameof(Phases.WaitingToStart))
		{
			var isMultiHandContinuation = existingPot is not null && existingPot.Amount > 0;

			// Multi-hand variants (like Kings and Lows) skip standard ante collection, so
			// the first hand's pot is 0. After that showdown, losers match 0 and no next-hand
			// pot is created. This doesn't mean the variant ended — it means the first hand had
			// no funded pot. Detect this by checking if the CURRENT hand's pot was non-trivial
			// (Amount > 0 and awarded). If it wasn't, this is the first hand and we should continue.
			if (!isMultiHandContinuation && flowHandler.IsMultiHandVariant)
			{
				var currentHandHadFundedPot = await context.Pots
					.AnyAsync(p => p.GameId == game.Id &&
					               p.HandNumber == game.CurrentHandNumber &&
					               p.PotType == PotType.Main &&
					               p.IsAwarded &&
					               p.Amount > 0,
					           cancellationToken);

				if (!currentHandHadFundedPot)
				{
					isMultiHandContinuation = true;
					_logger.LogInformation(
						"[DC-CHECK] Game {GameId}: Multi-hand variant {GameType} continues — " +
						"current hand had no funded pot (first hand of variant), antes will be collected",
						game.Id, game.CurrentHandGameTypeCode);
				}
			}

			_logger.LogInformation(
				"[DC-CHECK] Game {GameId}: HandNumber={HandNumber}, ExistingPot={PotExists} (Amount={PotAmount}), " +
				"IsMultiHandContinuation={IsContinuation}, GameType={GameType}, Phase={Phase}",
				game.Id,
				game.CurrentHandNumber,
				existingPot is not null,
				existingPot?.Amount ?? 0,
				isMultiHandContinuation,
				game.CurrentHandGameTypeCode,
				game.CurrentPhase);

			if (!isMultiHandContinuation)
			{
				// Current game variant is done — rotate DC dealer and wait for the next choice
				await TransitionDealersChoiceToWaitingForChoiceAsync(
					context,
					broadcaster,
					game,
					now,
					cancellationToken,
					"Dealer's Choice game {GameId}: variant finished, waiting for dealer at seat {DcDealerSeat} to choose next game");
				return OutcomeSkipped;
			}

			_logger.LogInformation(
				"Dealer's Choice game {GameId}: multi-hand variant continues (pot carried forward), same dealer at seat {DcDealerSeat}",
				game.Id,
				game.DealersChoiceDealerPosition);
		}

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
		// For Kings and Lows, antes are only collected on the first hand when the pot is empty.
		// Other multi-hand variants (for example Screw Your Neighbor) should not be auto-anted here.
		var shouldCollectAntes = !flowHandler.SkipsAnteCollection;
		if (!shouldCollectAntes &&
		    flowHandler.IsMultiHandVariant &&
		    string.Equals(flowHandler.GameTypeCode, PokerGameMetadataRegistry.KingsAndLowsCode, StringComparison.OrdinalIgnoreCase) &&
		    ante > 0)
		{
			var handPot = await context.Pots
				.FirstOrDefaultAsync(p => p.GameId == game.Id &&
				                          p.HandNumber == game.CurrentHandNumber &&
				                          p.PotType == PotType.Main,
				                     cancellationToken);

			if (handPot is not null && handPot.Amount == 0)
			{
				shouldCollectAntes = true;
				_logger.LogInformation(
					"Multi-hand variant {GameType} game {GameId}: collecting antes for first hand {HandNumber} (pot is empty)",
					flowHandler.GameTypeCode, game.Id, game.CurrentHandNumber);
			}
		}

		if (shouldCollectAntes)
		{
			await CollectAntesAsync(context, game, eligiblePlayers, ante, now, cancellationToken);
		}

		// Automatically deal hands using the flow handler
		await flowHandler.DealCardsAsync(context, game, eligiblePlayers, now, cancellationToken);
		await context.SaveChangesAsync(cancellationToken);

		if (await ShouldBroadcastScrewYourNeighborNewDeckToastAsync(context, flowHandler, game, cancellationToken))
		{
			await broadcaster.BroadcastTableToastAsync(
				new TableToastNotificationDto
				{
					GameId = game.Id,
					Message = "Starting new deck"
				},
				cancellationToken);
		}

		// Broadcast updated state
		await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
		return OutcomeAdvanced;
	}

	private static void SetBustedPlayersToObserve(IEnumerable<GamePlayer> gamePlayers)
	{
		foreach (var gamePlayer in gamePlayers.Where(gp =>
				 gp.Status is GamePlayerStatus.Active or GamePlayerStatus.SittingOut &&
				 gp.LeftAtHandNumber == -1 &&
				 gp.ChipStack <= 0))
		{
			gamePlayer.IsSittingOut = true;
			gamePlayer.Status = GamePlayerStatus.SittingOut;
		}
	}

	private static void MarkTournamentBustedPlayersAsObservers(IEnumerable<GamePlayer> gamePlayers, DateTimeOffset eliminatedAt)
	{
		foreach (var gamePlayer in gamePlayers)
		{
			gamePlayer.IsSittingOut = true;
			gamePlayer.Status = GamePlayerStatus.Eliminated;
			gamePlayer.HasFolded = true;
			gamePlayer.IsAllIn = false;
			gamePlayer.FinalChipCount = 0;
			gamePlayer.LeftAt ??= eliminatedAt;
		}
	}

	private async Task<bool> TryCompleteTournamentWhenOneFundedPlayerRemainsAsync(
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		LeagueGameCompletionSyncService? leagueCompletionSync,
		Game game,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (!game.TournamentBuyIn.HasValue)
		{
			return false;
		}

		var playersWithChips = game.GamePlayers
			.Where(gp => gp.Status != GamePlayerStatus.Left &&
						 gp.LeftAtHandNumber == -1 &&
						 gp.ChipStack > 0)
			.ToList();

		if (playersWithChips.Count > 1)
		{
			return false;
		}

		var winner = playersWithChips.SingleOrDefault();
		game.CurrentPhase = "Ended";
		game.Status = GameStatus.Completed;
		game.EndedAt = now;
		game.HandCompletedAt = now;
		game.NextHandStartsAt = null;
		game.IsPausedForChipCheck = false;
		game.ChipCheckPauseStartedAt = null;
		game.ChipCheckPauseEndsAt = null;
		game.IsPausedForRebuyGrace = false;
		game.RebuyGraceStartedAt = null;
		game.RebuyGraceEndsAt = null;
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);
		await SyncLeagueCompletionIfNeededAsync(leagueCompletionSync, game.Id, cancellationToken);

		var toastMessage = winner is null
			? "Tournament ended. No players had chips remaining."
			: $"Tournament complete. {winner.Player?.Name ?? $"Seat {winner.SeatPosition + 1}"} wins.";

		await broadcaster.BroadcastTableToastAsync(
			new TableToastNotificationDto
			{
				GameId = game.Id,
				Message = toastMessage,
				Type = "success",
				DurationMs = 6000
			},
			cancellationToken);
		await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
		return true;
	}

	private async Task<bool> TryTransitionTerminalDealersChoiceScrewYourNeighborAsync(
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		Game game,
		IGameFlowHandler flowHandler,
		int eligiblePlayerCount,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (!game.IsDealersChoice ||
			game.CurrentPhase == nameof(Phases.WaitingToStart) ||
			eligiblePlayerCount >= 2 ||
			!string.Equals(game.CurrentHandGameTypeCode, PokerGameMetadataRegistry.ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase) ||
			!flowHandler.IsMultiHandVariant)
		{
			return false;
		}

		var nextHandPotExists = await context.Pots
			.AnyAsync(p => p.GameId == game.Id &&
			               p.HandNumber == game.CurrentHandNumber + 1 &&
			               p.PotType == PotType.Main &&
			               p.Amount > 0,
			           cancellationToken);

		if (nextHandPotExists)
		{
			return false;
		}

		var currentHandHadFundedPot = await context.Pots
			.AnyAsync(p => p.GameId == game.Id &&
			               p.HandNumber == game.CurrentHandNumber &&
			               p.PotType == PotType.Main &&
			               p.IsAwarded &&
			               p.Amount > 0,
			           cancellationToken);

		if (!currentHandHadFundedPot)
		{
			return false;
		}

		await TransitionDealersChoiceToWaitingForChoiceAsync(
			context,
			broadcaster,
			game,
			now,
			cancellationToken,
			"Dealer's Choice game {GameId}: Screw Your Neighbor finished with one remaining stack, waiting for dealer at seat {DcDealerSeat} to choose next game");

		return true;
	}

	private async Task<bool> TryTransitionTerminalDealersChoiceInBetweenAsync(
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		Game game,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (!game.IsDealersChoice ||
			!string.Equals(game.CurrentHandGameTypeCode, PokerGameMetadataRegistry.InBetweenCode, StringComparison.OrdinalIgnoreCase) ||
			game.CurrentPhase != nameof(Phases.Complete))
		{
			return false;
		}

		// Clear In-Between variant state stored in GameSettings
		game.GameSettings = null;

		await TransitionDealersChoiceToWaitingForChoiceAsync(
			context,
			broadcaster,
			game,
			now,
			cancellationToken,
			"Dealer's Choice game {GameId}: In-Between finished (pot empty), waiting for dealer at seat {DcDealerSeat} to choose next game");

		return true;
	}

	private async Task TransitionDealersChoiceToWaitingForChoiceAsync(
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		Game game,
		DateTimeOffset now,
		CancellationToken cancellationToken,
		string logMessage)
	{
		// Restore pre-SYN DC chip stacks before clearing game settings.
		RestoreDcChipsAfterSyn(game);

		AdvanceDealersChoiceDealer(game);

		game.GameTypeId = null;
		game.CurrentHandGameTypeCode = null;
		game.Ante = null;
		game.MinBet = null;
		game.CurrentPhase = nameof(Phases.WaitingForDealerChoice);
		game.Status = GameStatus.BetweenHands;
		game.HandCompletedAt = null;
		game.NextHandStartsAt = null;
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);
		await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);

		_logger.LogInformation(logMessage, game.Id, game.DealersChoiceDealerPosition);
	}

	/// <summary>
	/// Restores pre-SYN Dealer's Choice chip stacks from VariantState after a SYN variant ends.
	/// Each player's DC chips are adjusted by their net SYN result (win or loss).
	/// </summary>
	private static void RestoreDcChipsAfterSyn(Game game)
	{
		var synAnte = game.Ante ?? 0;
		if (synAnte <= 0)
			return;

		var synBuyIn = synAnte * 3;

		foreach (var gp in game.GamePlayers)
		{
			if (string.IsNullOrEmpty(gp.VariantState))
				continue;

			try
			{
				using var doc = JsonDocument.Parse(gp.VariantState);
				if (doc.RootElement.TryGetProperty("preSynChips", out var preSynChipsElement))
				{
					var preSynChips = preSynChipsElement.GetInt32();
					var synNetResult = gp.ChipStack - synBuyIn;
					gp.ChipStack = Math.Max(0, preSynChips + synNetResult);

					// Un-sit-out players who still have DC chips after SYN
					if (gp.ChipStack > 0 && gp.IsSittingOut)
						gp.IsSittingOut = false;
				}
			}
			catch (JsonException)
			{
				// Non-SYN variant state — leave unchanged
			}

			gp.VariantState = null;
		}
	}

	private static async Task<bool> ShouldBroadcastScrewYourNeighborNewDeckToastAsync(
		CardsDbContext context,
		IGameFlowHandler flowHandler,
		Game game,
		CancellationToken cancellationToken)
	{
		if (!string.Equals(flowHandler.GameTypeCode, PokerGameMetadataRegistry.ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase) ||
			game.CurrentHandNumber <= 1)
		{
			return false;
		}

		var currentHandCardCount = await context.GameCards
			.CountAsync(gc => gc.GameId == game.Id && gc.HandNumber == game.CurrentHandNumber, cancellationToken);

		return currentHandCardCount == 52;
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
