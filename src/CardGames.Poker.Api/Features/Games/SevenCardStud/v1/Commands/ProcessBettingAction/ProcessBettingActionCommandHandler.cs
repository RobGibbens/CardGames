using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.SevenCardStud;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using BettingRound = CardGames.Poker.Api.Data.Entities.BettingRound;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction;

/// <summary>
/// Handles the <see cref="ProcessBettingActionCommand"/> to process betting actions from players.
/// </summary>
public class ProcessBettingActionCommandHandler(
	CardsDbContext context,
	IMediator mediator,
	ILogger<ProcessBettingActionCommandHandler> logger)
	: IRequestHandler<ProcessBettingActionCommand, OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>>
{
	/// <inheritdoc />
	public async Task<OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>> Handle(
		ProcessBettingActionCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		logger.LogDebug(
			"Processing betting action for game {GameId}, action type {ActionType}, amount {Amount}",
			command.GameId, command.ActionType, command.Amount);

		// 1. Load the game with its players and active betting round for the current hand
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.Pots)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new ProcessBettingActionError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = ProcessBettingActionErrorCode.GameNotFound
			};
		}

		logger.LogDebug(
			"Game loaded: Hand {HandNumber}, Phase {Phase}, PlayerIndex {PlayerIndex}",
			game.CurrentHandNumber, game.CurrentPhase, game.CurrentPlayerIndex);

		// Load the active betting round for the current hand separately to avoid filtered include issues
		var bettingRounds = await context.BettingRounds
			.Where(br => br.GameId == game.Id &&
						 br.HandNumber == game.CurrentHandNumber &&
						 !br.IsComplete)
			.Include(br => br.Actions)
			.OrderByDescending(br => br.RoundNumber)
			.ToListAsync(cancellationToken);

		logger.LogDebug(
			"Loaded {Count} active betting rounds for game {GameId}, hand {HandNumber}",
			bettingRounds.Count, game.Id, game.CurrentHandNumber);

		// 2. Validate game is in a betting phase
		var validBettingPhases = new[]
		{
			nameof(Phases.ThirdStreet),
			nameof(Phases.FourthStreet),
			nameof(Phases.FifthStreet),
			nameof(Phases.SixthStreet),
			nameof(Phases.SeventhStreet),
			nameof(Phases.Showdown)
		};

		if (!validBettingPhases.Contains(game.CurrentPhase))
		{
			return new ProcessBettingActionError
			{
				Message = $"Cannot process betting action. Game is in '{game.CurrentPhase}' phase. " +
						  $"Betting is only allowed during '{nameof(Phases.ThirdStreet)}' or '{nameof(Phases.FourthStreet)}' phases.",
				Code = ProcessBettingActionErrorCode.InvalidGameState
			};
		}

		// 3. Get the active betting round (already filtered by HandNumber and IsComplete when loaded)
		var bettingRound = bettingRounds.FirstOrDefault();
		if (bettingRound is null)
		{
			// Log diagnostic information to help debug why no betting round was found
			var allRoundsForGame = await context.BettingRounds
				.Where(br => br.GameId == game.Id)
				.Select(br => new { br.HandNumber, br.RoundNumber, br.Street, br.IsComplete })
				.ToListAsync(cancellationToken);

			logger.LogWarning(
				"No active betting round found for game {GameId}, hand {HandNumber}, phase {Phase}. " +
				"Total betting rounds in game: {TotalRounds}. Rounds: {Rounds}",
				game.Id,
				game.CurrentHandNumber,
				game.CurrentPhase,
				allRoundsForGame.Count,
				string.Join(", ", allRoundsForGame.Select(r => $"[Hand:{r.HandNumber}, Round:{r.RoundNumber}, Street:{r.Street}, Complete:{r.IsComplete}]")));

			return new ProcessBettingActionError
			{
				Message = $"No active betting round found for hand {game.CurrentHandNumber} in phase '{game.CurrentPhase}'. " +
						  $"Total betting rounds in database: {allRoundsForGame.Count}.",
				Code = ProcessBettingActionErrorCode.NoBettingRound
			};
		}

		// 4. Get active players ordered by seat position
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		// 5. Get current player
		var currentPlayer = activePlayers.FirstOrDefault(gp => gp.SeatPosition == bettingRound.CurrentActorIndex);
		if (currentPlayer is null)
		{
			return new ProcessBettingActionError
			{
				Message = "Could not determine current player.",
				Code = ProcessBettingActionErrorCode.InvalidGameState
			};
		}

		// 6. Validate and process the action
		var validationResult = ValidateAction(command.ActionType, command.Amount, currentPlayer, bettingRound);
		if (validationResult is not null)
		{
			return new ProcessBettingActionError
			{
				Message = validationResult,
				Code = ProcessBettingActionErrorCode.InvalidAction
			};
		}

		// 7. Calculate values before execution
		var chipStackBefore = currentPlayer.ChipStack;
		var potBefore = game.Pots.Sum(p => p.Amount);

		// 8. Execute the action
		var (actualAmount, chipsMoved) = ExecuteAction(currentPlayer, command.ActionType, command.Amount, bettingRound);

		// 9. Create action record
		var actionRecord = new BettingActionRecord
		{
			BettingRoundId = bettingRound.Id,
			GamePlayerId = currentPlayer.Id,
			ActionOrder = bettingRound.Actions.Count + 1,
			ActionType = command.ActionType,
			Amount = actualAmount,
			ChipsMoved = chipsMoved,
			ChipStackBefore = chipStackBefore,
			ChipStackAfter = currentPlayer.ChipStack,
			PotBefore = potBefore,
			PotAfter = potBefore + chipsMoved,
			IsForced = false,
			IsTimeout = false,
			ActionAt = now
		};

		context.BettingActionRecords.Add(actionRecord);

		// 10. Update pot (must filter by current hand number to avoid updating old pots)
		var mainPot = game.Pots.FirstOrDefault(p => p.PotOrder == 0 && p.HandNumber == game.CurrentHandNumber);
		if (mainPot is not null)
		{
			mainPot.Amount += chipsMoved;
		}

		// 11. Track that player has acted and update betting round state
		bettingRound.PlayersActed++;

		// 12. Determine next player and check if round is complete
		var roundComplete = false;
		var nextPlayerIndex = -1;
		string? nextPlayerName = null;

		// Move to next active player
		nextPlayerIndex = FindNextActivePlayer(activePlayers, bettingRound.CurrentActorIndex);

		// Check if betting round is complete
		roundComplete = IsRoundComplete(activePlayers, bettingRound, nextPlayerIndex);

		if (roundComplete)
		{
			bettingRound.IsComplete = true;
			bettingRound.CompletedAt = now;
			nextPlayerIndex = -1;

			// Check if all players are all-in (fewer than 2 players can bet)
			// If so, deal remaining cards without betting and go to showdown
			var allPlayersAllIn = AreAllPlayersAllIn(activePlayers);

			if (allPlayersAllIn)
			{
				logger.LogInformation(
					"All players are all-in after {Phase} for game {GameId}. Dealing remaining streets and proceeding to showdown.",
					game.CurrentPhase, game.Id);

				// Advance to next phase
				var previousPhase = game.CurrentPhase;
				AdvanceToNextPhase(game, activePlayers);

				// If not already at showdown, deal remaining streets
				if (game.CurrentPhase != nameof(Phases.Showdown))
				{
					await DealRemainingStreetsAsync(game, activePlayers, game.CurrentPhase, now, cancellationToken);
				}

				game.UpdatedAt = now;
				await context.SaveChangesAsync(cancellationToken);

				return new ProcessBettingActionSuccessful
				{
					GameId = game.Id,
					RoundComplete = true,
					CurrentPhase = game.CurrentPhase,
					Action = new BettingActionResult
					{
						PlayerName = currentPlayer.Player.Name,
						ActionType = command.ActionType,
						Amount = actualAmount,
						ChipStackAfter = currentPlayer.ChipStack
					},
					PlayerSeatIndex = currentPlayer.SeatPosition,
					NextPlayerIndex = -1,
					NextPlayerName = null,
					PotTotal = game.Pots.Sum(p => p.Amount),
					CurrentBet = bettingRound.CurrentBet
				};
			}

			// Advance to next phase
			var previousPhaseNormal = game.CurrentPhase;
			AdvanceToNextPhase(game, activePlayers);

			logger.LogInformation(
				"Betting round complete. Advancing from {PreviousPhase} to {NewPhase} for game {GameId}, hand {HandNumber}",
				previousPhaseNormal, game.CurrentPhase, game.Id, game.CurrentHandNumber);

			// 13. Update timestamps
			game.UpdatedAt = now;

			// 14. Check if we need to deal next street cards
			var streetPhases = new[]
			{
					nameof(Phases.FourthStreet),
					nameof(Phases.FifthStreet),
					nameof(Phases.SixthStreet),
					nameof(Phases.SeventhStreet)
				};

			if (streetPhases.Contains(game.CurrentPhase))
			{
				// Use the execution strategy to handle retries with transactions
				var executionStrategy = context.Database.CreateExecutionStrategy();

				// Track deal result outside the execution strategy scope
				OneOf<DealHandsSuccessful, DealHandsError>? dealResultHolder = null;

				try
				{
					await executionStrategy.ExecuteAsync(async ct =>
					{
						// Use a transaction to ensure the phase transition and dealing happen atomically
						await using var transaction = await context.Database.BeginTransactionAsync(ct);

						// Persist the completed betting round and phase change
						await context.SaveChangesAsync(ct);

						logger.LogDebug(
							"Calling DealHandsCommand for {Phase} on game {GameId}",
							game.CurrentPhase, game.Id);

						// Deal the next street cards and create the betting round
						dealResultHolder = await mediator.Send(new DealHandsCommand(game.Id), ct);

						if (dealResultHolder.Value.IsT0) // Success
						{
							// Commit the transaction
							await transaction.CommitAsync(ct);
						}
						else
						{
							// Deal failed - rollback the transaction
							await transaction.RollbackAsync(ct);
						}
					}, cancellationToken);

					// Process the deal result after the transaction completes
					if (dealResultHolder?.IsT0 == true)
					{
						var dealSuccess = dealResultHolder.Value.AsT0;
						nextPlayerIndex = dealSuccess.CurrentPlayerIndex;
						nextPlayerName = dealSuccess.CurrentPlayerName;

						// Update phase from deal result to ensure consistency
						game.CurrentPhase = dealSuccess.CurrentPhase;
					}
					else if (dealResultHolder?.IsT1 == true)
					{
						var dealError = dealResultHolder.Value.AsT1;
						logger.LogError(
							"Failed to deal next street for game {GameId}, phase {Phase}: {ErrorMessage}",
							game.Id, game.CurrentPhase, dealError.Message);

						return new ProcessBettingActionError
						{
							Message = $"Failed to deal next street: {dealError.Message}",
							Code = ProcessBettingActionErrorCode.InvalidGameState
						};
					}
				}
				catch (Exception ex)
				{
					logger.LogError(ex,
						"Exception during phase transition for game {GameId}, phase {Phase}",
						game.Id, game.CurrentPhase);
					throw;
				}
			}
			else
			{
				// Not advancing to a new street (e.g., going to Showdown), just save
				await context.SaveChangesAsync(cancellationToken);
			}
		}
		else
		{
			bettingRound.CurrentActorIndex = nextPlayerIndex;
			game.CurrentPlayerIndex = nextPlayerIndex;
			nextPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == nextPlayerIndex)?.Player.Name;

			// 13. Update timestamps
			game.UpdatedAt = now;

			// 14. Persist changes
			await context.SaveChangesAsync(cancellationToken);
		}

		return new ProcessBettingActionSuccessful
		{
			GameId = game.Id,
			RoundComplete = roundComplete,
			CurrentPhase = game.CurrentPhase,
			Action = new BettingActionResult
			{
				PlayerName = currentPlayer.Player.Name,
				ActionType = command.ActionType,
				Amount = actualAmount,
				ChipStackAfter = currentPlayer.ChipStack
			},
			PlayerSeatIndex = currentPlayer.SeatPosition,
			NextPlayerIndex = nextPlayerIndex,
			NextPlayerName = nextPlayerName,
			PotTotal = game.Pots.Sum(p => p.Amount),
			CurrentBet = bettingRound.CurrentBet
		};
	}

	private string? ValidateAction(BettingActionType actionType, int amount, GamePlayer player, BettingRound bettingRound)
	{
		var amountToCall = Math.Max(0, bettingRound.CurrentBet - player.CurrentBet);

		return actionType switch
		{
			BettingActionType.Check when bettingRound.CurrentBet > player.CurrentBet =>
				"Cannot check - there is a bet to match",
			BettingActionType.Bet when bettingRound.CurrentBet > 0 =>
				"Cannot bet - there is already a bet. Use raise instead.",
			BettingActionType.Bet when amount < bettingRound.MinBet =>
				$"Bet must be at least {bettingRound.MinBet}",
			BettingActionType.Bet when amount > player.ChipStack =>
				$"Cannot bet more than your stack ({player.ChipStack})",
			BettingActionType.Call when bettingRound.CurrentBet <= player.CurrentBet =>
				"Cannot call - no bet to match",
			BettingActionType.Raise when bettingRound.CurrentBet == 0 =>
				"Cannot raise - no bet to raise. Use bet instead.",
			BettingActionType.Raise when amount < bettingRound.CurrentBet + bettingRound.LastRaiseAmount
										 && amount < player.ChipStack + player.CurrentBet =>
				$"Raise must be to at least {bettingRound.CurrentBet + bettingRound.LastRaiseAmount}",
			BettingActionType.Fold when bettingRound.CurrentBet == player.CurrentBet =>
				"Cannot fold when you can check",
			BettingActionType.AllIn when player.ChipStack == 0 =>
				"Cannot go all-in - no chips remaining",
			_ => null
		};
	}

	private (int actualAmount, int chipsMoved) ExecuteAction(
		GamePlayer player,
		BettingActionType actionType,
		int amount,
		BettingRound bettingRound)
	{
		int actualAmount = 0;
		int chipsMoved = 0;

		switch (actionType)
		{
			case BettingActionType.Check:
				// No chips change hands
				break;

			case BettingActionType.Bet:
				actualAmount = amount;
				chipsMoved = amount;
				player.ChipStack -= amount;
				player.CurrentBet += amount;
				player.TotalContributedThisHand += amount;
				bettingRound.CurrentBet = amount;
				bettingRound.LastRaiseAmount = amount;
				bettingRound.LastAggressorIndex = player.SeatPosition;
				bettingRound.RaiseCount++;
				if (player.ChipStack == 0) player.IsAllIn = true;
				break;

			case BettingActionType.Call:
				var callAmount = Math.Min(bettingRound.CurrentBet - player.CurrentBet, player.ChipStack);
				actualAmount = callAmount;
				chipsMoved = callAmount;
				player.ChipStack -= callAmount;
				player.CurrentBet += callAmount;
				player.TotalContributedThisHand += callAmount;
				if (player.ChipStack == 0) player.IsAllIn = true;
				break;

			case BettingActionType.Raise:
				var raiseTotal = amount;
				var raiseChips = raiseTotal - player.CurrentBet;
				actualAmount = raiseTotal;
				chipsMoved = raiseChips;
				var raiseIncrement = raiseTotal - bettingRound.CurrentBet;
				player.ChipStack -= raiseChips;
				player.CurrentBet = raiseTotal;
				player.TotalContributedThisHand += raiseChips;
				bettingRound.LastRaiseAmount = raiseIncrement;
				bettingRound.CurrentBet = raiseTotal;
				bettingRound.LastAggressorIndex = player.SeatPosition;
				bettingRound.RaiseCount++;
				if (player.ChipStack == 0) player.IsAllIn = true;
				break;

			case BettingActionType.Fold:
				player.HasFolded = true;
				break;

			case BettingActionType.AllIn:
				var allInAmount = player.ChipStack;
				actualAmount = allInAmount + player.CurrentBet;
				chipsMoved = allInAmount;

				// If this is a raise (putting in more than current bet)
				if (player.CurrentBet + allInAmount > bettingRound.CurrentBet)
				{
					var newTotal = player.CurrentBet + allInAmount;
					var raiseBy = newTotal - bettingRound.CurrentBet;
					if (raiseBy >= bettingRound.LastRaiseAmount)
					{
						bettingRound.LastRaiseAmount = raiseBy;
						bettingRound.LastAggressorIndex = player.SeatPosition;
						bettingRound.RaiseCount++;
					}
					bettingRound.CurrentBet = newTotal;
				}

				player.CurrentBet += allInAmount;
				player.TotalContributedThisHand += allInAmount;
				player.ChipStack = 0;
				player.IsAllIn = true;
				break;
		}

		return (actualAmount, chipsMoved);
	}

	private static int FindNextActivePlayer(List<GamePlayer> activePlayers, int currentIndex)
	{
		var totalPlayers = activePlayers.Max(p => p.SeatPosition) + 1;
		var searchIndex = (currentIndex + 1) % totalPlayers;

		for (var i = 0; i < totalPlayers; i++)
		{
			var player = activePlayers.FirstOrDefault(p => p.SeatPosition == searchIndex);
			if (player is not null && !player.HasFolded && !player.IsAllIn)
			{
				return searchIndex;
			}
			searchIndex = (searchIndex + 1) % totalPlayers;
		}

		return -1; // No active player found
	}

	private bool IsRoundComplete(List<GamePlayer> activePlayers, BettingRound bettingRound, int nextPlayerIndex)
	{
		// Round is complete if only one player remains
		var playersInHand = activePlayers.Count(p => !p.HasFolded);
		if (playersInHand <= 1) return true;

		// Round is complete if all non-folded, non-all-in players have matched the current bet
		var playersWhoCanAct = activePlayers.Where(p => !p.HasFolded && !p.IsAllIn).ToList();

		// If no players can act (all are all-in or folded), round is complete
		if (playersWhoCanAct.Count == 0) return true;

		// Check if all players have matched the current bet and have had a chance to act
		var allMatched = playersWhoCanAct.All(p => p.CurrentBet == bettingRound.CurrentBet);

		// If there's a last aggressor, we need to complete the round back to them
		if (bettingRound.LastAggressorIndex >= 0 && nextPlayerIndex == bettingRound.LastAggressorIndex && allMatched)
		{
			return true;
		}

		// If everyone has acted at least once and all bets are matched
		if (bettingRound.PlayersActed >= playersWhoCanAct.Count && allMatched)
		{
			return true;
		}

		return false;
	}

	private void AdvanceToNextPhase(Game game, List<GamePlayer> activePlayers)
	{
		// Check if only one player remains (all others folded)
		var playersInHand = activePlayers.Count(gp => !gp.HasFolded);
		if (playersInHand <= 1)
		{
			game.CurrentPhase = nameof(Phases.Showdown);
			game.CurrentPlayerIndex = -1;
			return;
		}

		// Reset current bets for next round
		foreach (var gamePlayer in activePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		// Seven Card Stud phase progression:
		// ThirdStreet (betting) -> FourthStreet (deal card, then betting)
		// FourthStreet (betting) -> FifthStreet (deal card, then betting)
		// FifthStreet (betting) -> SixthStreet (deal card, then betting)
		// SixthStreet (betting) -> SeventhStreet (deal card, then betting)
		// SeventhStreet (betting) -> Showdown
		switch (game.CurrentPhase)
		{
			case nameof(Phases.ThirdStreet):
				game.CurrentPhase = nameof(Phases.FourthStreet);
				game.CurrentDrawPlayerIndex = FindFirstActivePlayerAfterDealer(game, activePlayers);
				game.CurrentPlayerIndex = game.CurrentDrawPlayerIndex;
				break;

			case nameof(Phases.FourthStreet):
				game.CurrentPhase = nameof(Phases.FifthStreet);
				game.CurrentDrawPlayerIndex = FindFirstActivePlayerAfterDealer(game, activePlayers);
				game.CurrentPlayerIndex = game.CurrentDrawPlayerIndex;
				break;

			case nameof(Phases.FifthStreet):
				game.CurrentPhase = nameof(Phases.SixthStreet);
				game.CurrentDrawPlayerIndex = FindFirstActivePlayerAfterDealer(game, activePlayers);
				game.CurrentPlayerIndex = game.CurrentDrawPlayerIndex;
				break;

			case nameof(Phases.SixthStreet):
				game.CurrentPhase = nameof(Phases.SeventhStreet);
				game.CurrentDrawPlayerIndex = FindFirstActivePlayerAfterDealer(game, activePlayers);
				game.CurrentPlayerIndex = game.CurrentDrawPlayerIndex;
				break;

			case nameof(Phases.SeventhStreet):
				game.CurrentPhase = nameof(Phases.Showdown);
				game.CurrentPlayerIndex = -1;
				break;
		}
	}

	private int FindFirstActivePlayerAfterDealer(Game game, List<GamePlayer> activePlayers)
	{
		var totalPlayers = activePlayers.Max(p => p.SeatPosition) + 1;
		var searchIndex = (game.DealerPosition + 1) % totalPlayers;

		for (var i = 0; i < totalPlayers; i++)
		{
			var player = activePlayers.FirstOrDefault(p => p.SeatPosition == searchIndex);
			if (player is not null && !player.HasFolded && !player.IsAllIn)
			{
				return searchIndex;
			}
			searchIndex = (searchIndex + 1) % totalPlayers;
		}

		return -1;
	}

	/// <summary>
	/// Determines if all remaining players are all-in, meaning no further betting can occur.
	/// This happens when fewer than 2 non-folded players have chips remaining to bet.
	/// </summary>
	/// <param name="activePlayers">The list of active players in the game.</param>
	/// <returns>True if fewer than 2 non-folded players can still bet, meaning betting should skip to showdown.</returns>
	private static bool AreAllPlayersAllIn(List<GamePlayer> activePlayers)
	{
		// First, count how many players are still in the hand (not folded)
		var playersInHand = activePlayers.Count(p => !p.HasFolded);

		// If only one player remains, this is a win by fold scenario, not an all-in scenario
		// The caller should handle this separately via AdvanceToNextPhase
		if (playersInHand <= 1)
		{
			return false;
		}

		// Count players who haven't folded and still have chips to bet
		// A player can bet if they're not all-in and have chips remaining
		var playersWhoCanBet = activePlayers.Count(p => !p.HasFolded && !p.IsAllIn && p.ChipStack > 0);

		// If fewer than 2 players can bet, all remaining betting rounds should be skipped
		// and the remaining streets should be dealt without betting
		return playersWhoCanBet < 2;
	}

	/// <summary>
	/// Deals all remaining street cards to players when all players are all-in.
	/// This allows the hand to run out to showdown without betting.
	/// </summary>
	/// <param name="game">The current game.</param>
	/// <param name="activePlayers">Players who are still in the hand (not folded).</param>
	/// <param name="currentPhase">The phase to start dealing from.</param>
	/// <param name="now">The current timestamp.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task DealRemainingStreetsAsync(
		Game game,
		List<GamePlayer> activePlayers,
		string currentPhase,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// Define the street order for Seven Card Stud
		var streetOrder = new[]
		{
					nameof(Phases.FourthStreet),
					nameof(Phases.FifthStreet),
					nameof(Phases.SixthStreet),
					nameof(Phases.SeventhStreet)
				};

		// Find which streets still need to be dealt
		var startIndex = Array.IndexOf(streetOrder, currentPhase);
		if (startIndex < 0)
		{
			// Already past all streets or not a street phase
			return;
		}

		// Get players who haven't folded (they may be all-in)
		var playersToReceiveCards = activePlayers.Where(p => !p.HasFolded).OrderBy(p => p.SeatPosition).ToList();

		// Load remaining deck cards
		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
						 gc.HandNumber == game.CurrentHandNumber &&
						 gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		var deckIndex = 0;

		// Deal cards for each remaining street
		for (var streetIdx = startIndex; streetIdx < streetOrder.Length; streetIdx++)
		{
			var street = streetOrder[streetIdx];

			foreach (var player in playersToReceiveCards)
			{
				if (deckIndex >= deckCards.Count)
				{
					logger.LogWarning(
						"Not enough cards in deck to deal remaining streets for game {GameId}",
						game.Id);
					break;
				}

				var existingCardCount = await context.GameCards
					.CountAsync(gc => gc.GamePlayerId == player.Id &&
									  gc.HandNumber == game.CurrentHandNumber &&
									  gc.Location != CardLocation.Deck &&
									  !gc.IsDiscarded, cancellationToken);

				var playerDealOrder = existingCardCount + 1;
				var gameCard = deckCards[deckIndex++];

				// 7th street is a hole card, all others are board cards
				var location = street == nameof(Phases.SeventhStreet) ? CardLocation.Hole : CardLocation.Board;
				var isVisible = street != nameof(Phases.SeventhStreet);

				gameCard.GamePlayerId = player.Id;
				gameCard.Location = location;
				gameCard.DealOrder = playerDealOrder;
				gameCard.IsVisible = isVisible;
				gameCard.DealtAt = now;
				gameCard.DealtAtPhase = street;
			}

			logger.LogInformation(
				"Dealt {Street} cards without betting (all players all-in) for game {GameId}",
				street, game.Id);
		}

		// Update game phase to Showdown
		game.CurrentPhase = nameof(Phases.Showdown);
		game.CurrentPlayerIndex = -1;
		game.UpdatedAt = now;

		logger.LogInformation(
			"All remaining streets dealt. Moving to Showdown for game {GameId}",
			game.Id);
	}
}

