using System.Text.Json;
using CardGames.Poker.Api.Betting;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using BettingRound = CardGames.Poker.Api.Data.Entities.BettingRound;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;

namespace CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;

/// <summary>
///     Handles the <see cref="ProcessBettingActionCommand" /> to process betting actions from players
///     in a Texas Hold 'Em game.
/// </summary>
public class ProcessBettingActionCommandHandler(
	CardsDbContext context,
	ILogger<ProcessBettingActionCommandHandler> logger,
	GameEventsTelemetry? telemetry = null)
	: IRequestHandler<ProcessBettingActionCommand, OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>>
{
	/// <summary>
	///     Valid Hold 'Em betting phases.
	/// </summary>
	private static readonly string[] ValidBettingPhases = ["PreFlop", "Flop", "Turn", "River"];

	/// <inheritdoc />
	public async Task<OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>> Handle(
		ProcessBettingActionCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		logger.LogDebug(
			"Processing Hold 'Em betting action for game {GameId}, action type {ActionType}, amount {Amount}",
			command.GameId, command.ActionType, command.Amount);

		// 1. Load the game with its players and pots
		var game = await context.Games
			.Include(g => g.GamePlayers)
			.ThenInclude(gp => gp.Player)
			.Include(g => g.GameType)
			.Include(g => g.Pots)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
			return new ProcessBettingActionError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = ProcessBettingActionErrorCode.GameNotFound
			};

		logger.LogDebug(
			"Game loaded: Hand {HandNumber}, Phase {Phase}, PlayerIndex {PlayerIndex}",
			game.CurrentHandNumber, game.CurrentPhase, game.CurrentPlayerIndex);

		// 2. Validate game is in a betting phase
		if (!ValidBettingPhases.Contains(game.CurrentPhase))
			return new ProcessBettingActionError
			{
				Message = $"Cannot process betting action. Game is in '{game.CurrentPhase}' phase. " +
				          "Betting is only allowed during PreFlop, Flop, Turn, or River phases.",
				Code = ProcessBettingActionErrorCode.InvalidGameState
			};

		// 3. Load the active betting round for the current hand
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

		var bettingRound = bettingRounds.FirstOrDefault();
		if (bettingRound is null)
		{
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
				string.Join(", ",
					allRoundsForGame.Select(r =>
						$"[Hand:{r.HandNumber}, Round:{r.RoundNumber}, Street:{r.Street}, Complete:{r.IsComplete}]")));

			return new ProcessBettingActionError
			{
				Message =
					$"No active betting round found for hand {game.CurrentHandNumber} in phase '{game.CurrentPhase}'. " +
					$"Total betting rounds in database: {allRoundsForGame.Count}.",
				Code = ProcessBettingActionErrorCode.NoBettingRound
			};
		}

		// Red River rule enforcement: if River is red, the bonus board card must be
		// available before any River betting action is processed.
		if (IsRedRiverGame(game) && string.Equals(game.CurrentPhase, "River", StringComparison.OrdinalIgnoreCase))
			await DealRedRiverBonusCommunityCardIfNeededAsync(game, now, cancellationToken);

		// 4. Get active players ordered by seat position
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		// 5. Get current player
		var currentPlayer = activePlayers.FirstOrDefault(gp => gp.SeatPosition == bettingRound.CurrentActorIndex);
		if (currentPlayer is null)
			return new ProcessBettingActionError
			{
				Message = "Could not determine current player.",
				Code = ProcessBettingActionErrorCode.InvalidGameState
			};

		// 6. Validate the action
		var actionType = NormalizeActionType(command.ActionType, game, currentPlayer, bettingRound);
		var validationResult = ValidateAction(actionType, command.Amount, currentPlayer, bettingRound);
		if (validationResult is not null)
			return new ProcessBettingActionError
			{
				Message = validationResult,
				Code = ProcessBettingActionErrorCode.InvalidAction
			};

		// 7. Calculate values before execution
		var chipStackBefore = currentPlayer.ChipStack;
		var potBefore = game.Pots.Sum(p => p.Amount);
		var playerWasAllIn = currentPlayer.IsAllIn;

		// 8. Execute the action
		var (actualAmount, chipsMoved) = ExecuteAction(currentPlayer, actionType, command.Amount, bettingRound);
		var playerWentAllIn = !playerWasAllIn && currentPlayer.IsAllIn;

		// 9. Create action record
		var actionRecord = new BettingActionRecord
		{
			BettingRoundId = bettingRound.Id,
			GamePlayerId = currentPlayer.Id,
			ActionOrder = bettingRound.Actions.Count + 1,
			ActionType = actionType,
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

		// 10. Update pot
		var mainPot = game.Pots.FirstOrDefault(p => p.PotOrder == 0 && p.HandNumber == game.CurrentHandNumber);
		if (mainPot is not null) mainPot.Amount += chipsMoved;

		// 11. Track that player has acted
		bettingRound.PlayersActed++;

		// 12. Determine next player and check if round is complete
		var roundComplete = false;
		var nextPlayerIndex = -1;
		string? nextPlayerName = null;

		nextPlayerIndex = FindNextActivePlayer(activePlayers, bettingRound.CurrentActorIndex);
		var actedPlayerIds = BettingRoundCompletion.ActedPlayerIds(bettingRound, currentPlayer.Id);
		roundComplete = BettingRoundCompletion.IsRoundComplete(
			activePlayers, bettingRound, nextPlayerIndex, actedPlayerIds);

		if (roundComplete)
		{
			bettingRound.IsComplete = true;
			bettingRound.CompletedAt = now;
			nextPlayerIndex = -1;

			// Check if all players are all-in
			var allPlayersAllIn = AreAllPlayersAllIn(activePlayers);

			if (allPlayersAllIn)
			{
				var closedPhase = game.CurrentPhase;
				var discardDecision =
					await TryEnterOwedDiscardPhaseAsync(game, activePlayers, now, cancellationToken);
				if (discardDecision.IsT1)
					return discardDecision.AsT1;

				if (discardDecision.AsT0)
				{
					logger.LogInformation(
						"Fewer than two players can bet after {Phase} for game {GameId}. Entering DrawPhase before the remaining board is dealt.",
						closedPhase, game.Id);

					game.UpdatedAt = now;
					await context.SaveChangesAsync(cancellationToken);
					if (playerWentAllIn)
					{
						telemetry?.RecordPlayerEvent(GameEventsTelemetry.ResolveGameType(game),
							GameEventsTelemetry.ResolveTableKind(game), "all_in");
					}

					var nextDrawPlayer =
						activePlayers.FirstOrDefault(p => p.SeatPosition == game.CurrentDrawPlayerIndex);
					return new ProcessBettingActionSuccessful
					{
						GameId = game.Id,
						RoundComplete = true,
						CurrentPhase = game.CurrentPhase,
						Action = new BettingActionResult
						{
							PlayerName = currentPlayer.Player.Name,
							ActionType = actionType,
							Amount = actualAmount,
							ChipStackAfter = currentPlayer.ChipStack
						},
						PlayerSeatIndex = currentPlayer.SeatPosition,
						NextPlayerIndex = game.CurrentDrawPlayerIndex,
						NextPlayerName = nextDrawPlayer?.Player.Name,
						PotTotal = game.Pots.Sum(p => p.Amount),
						CurrentBet = bettingRound.CurrentBet
					};
				}

				logger.LogInformation(
					"All players are all-in after {Phase} for game {GameId}. Dealing remaining community cards and proceeding to showdown.",
					closedPhase, game.Id);

				await DealRemainingCommunityCardsAsync(game, game.CurrentPhase, now, cancellationToken);

				game.UpdatedAt = now;
				await context.SaveChangesAsync(cancellationToken);
				if (playerWentAllIn)
				{
					telemetry?.RecordPlayerEvent(GameEventsTelemetry.ResolveGameType(game),
						GameEventsTelemetry.ResolveTableKind(game), "all_in");
				}

				return new ProcessBettingActionSuccessful
				{
					GameId = game.Id,
					RoundComplete = true,
					CurrentPhase = game.CurrentPhase,
					Action = new BettingActionResult
					{
						PlayerName = currentPlayer.Player.Name,
						ActionType = actionType,
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

			// Normal phase advancement
			var previousPhase = game.CurrentPhase;
			AdvanceToNextPhase(game, activePlayers);

			if (IsRedRiverGame(game)
			    && string.Equals(previousPhase, "River", StringComparison.OrdinalIgnoreCase)
			    && string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase))
				await DealRedRiverBonusCommunityCardIfNeededAsync(game, now, cancellationToken);

			logger.LogInformation(
				"Betting round complete. Advancing from {PreviousPhase} to {NewPhase} for game {GameId}, hand {HandNumber}",
				previousPhase, game.CurrentPhase, game.Id, game.CurrentHandNumber);

			// Irish Hold 'Em and Crazy Pineapple: when advancing to Flop, deal the flop
			// community cards but skip the Flop betting round and go directly to DrawPhase.
			if (game.CurrentPhase == "Flop" && (IsIrishHoldEmGame(game) || IsCrazyPineappleGame(game)))
			{
				// Deal flop community cards
				await DealCommunityCardsForPhaseAsync(game, "Flop", now, cancellationToken);

				// Skip Flop betting — go directly to DrawPhase
				game.CurrentPhase = "DrawPhase";
				var firstDrawPlayerIndex = FindFirstActivePlayerAfterDealer(game, activePlayers);
				game.CurrentDrawPlayerIndex = firstDrawPlayerIndex;
				game.CurrentPlayerIndex = firstDrawPlayerIndex;

				logger.LogInformation(
					"Flop dealt, skipping Flop betting round and entering DrawPhase for game {GameId}",
					game.Id);

				game.UpdatedAt = now;
				await context.SaveChangesAsync(cancellationToken);
				if (playerWentAllIn)
				{
					telemetry?.RecordPlayerEvent(GameEventsTelemetry.ResolveGameType(game),
						GameEventsTelemetry.ResolveTableKind(game), "all_in");
				}

				return new ProcessBettingActionSuccessful
				{
					GameId = game.Id,
					RoundComplete = true,
					CurrentPhase = game.CurrentPhase,
					Action = new BettingActionResult
					{
						PlayerName = currentPlayer.Player.Name,
						ActionType = actionType,
						Amount = actualAmount,
						ChipStackAfter = currentPlayer.ChipStack
					},
					PlayerSeatIndex = currentPlayer.SeatPosition,
					NextPlayerIndex = firstDrawPlayerIndex,
					NextPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == firstDrawPlayerIndex)?.Player
						.Name,
					PotTotal = game.Pots.Sum(p => p.Amount),
					CurrentBet = bettingRound.CurrentBet
				};
			}

			// Non-Irish Hold 'Em: entering discard phase — set first draw player and return
			if (game.CurrentPhase == "DrawPhase")
			{
				foreach (var player in activePlayers) player.HasDrawnThisRound = false;

				var firstDrawPlayerIndex = FindFirstActivePlayerAfterDealer(game, activePlayers);
				game.CurrentDrawPlayerIndex = firstDrawPlayerIndex;
				game.CurrentPlayerIndex = firstDrawPlayerIndex;

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
						ActionType = actionType,
						Amount = actualAmount,
						ChipStackAfter = currentPlayer.ChipStack
					},
					PlayerSeatIndex = currentPlayer.SeatPosition,
					NextPlayerIndex = firstDrawPlayerIndex,
					NextPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == firstDrawPlayerIndex)?.Player
						.Name,
					PotTotal = game.Pots.Sum(p => p.Amount),
					CurrentBet = bettingRound.CurrentBet
				};
			}

			// Deal community cards if advancing to Flop, Turn, or River
			if (game.CurrentPhase is "Flop" or "Turn" or "River")
			{
				await DealCommunityCardsForPhaseAsync(game, game.CurrentPhase, now, cancellationToken);

				// Red River rule: if the revealed river card is red, immediately expose
				// the sixth community card before any River betting actions occur.
				if (IsRedRiverGame(game) &&
				    string.Equals(game.CurrentPhase, "River", StringComparison.OrdinalIgnoreCase))
					await DealRedRiverBonusCommunityCardIfNeededAsync(game, now, cancellationToken);

				// Create a new betting round for the next phase
				var firstToActIndex = FindFirstActivePlayerAfterDealer(game, activePlayers);

				var newBettingRound = new BettingRound
				{
					GameId = game.Id,
					HandNumber = game.CurrentHandNumber,
					RoundNumber = bettingRound.RoundNumber + 1,
					Street = game.CurrentPhase,
					IsComplete = false,
					CurrentBet = 0,
					MinBet = game.BigBlind ?? game.MinBet ?? 0,
					CurrentActorIndex = firstToActIndex,
					PlayersActed = 0,
					PlayersInHand = activePlayers.Count(p => !p.HasFolded),
					MaxRaises = 4,
					LastRaiseAmount = game.BigBlind ?? game.MinBet ?? 0,
					LastAggressorIndex = -1,
					StartedAt = now,
					RaiseCount = 0
				};
				context.BettingRounds.Add(newBettingRound);

				game.CurrentPlayerIndex = firstToActIndex;
				nextPlayerIndex = firstToActIndex;
				nextPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == firstToActIndex)?.Player.Name;
			}
			else
			{
				// Going to Showdown — no more betting
				nextPlayerIndex = -1;
				nextPlayerName = null;
				game.CurrentPlayerIndex = -1;
			}

			if (IsRedRiverGame(game) &&
			    string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase))
				await DealRedRiverBonusCommunityCardIfNeededAsync(game, now, cancellationToken);

			// Klondike: reveal the wild card and record the timestamp for the timed delay
			if (IsKlondikeGame(game) &&
			    string.Equals(game.CurrentPhase, "KlondikeReveal", StringComparison.OrdinalIgnoreCase))
			{
				await RevealKlondikeCardAsync(game, now, cancellationToken);
				game.DrawCompletedAt = now;
			}

			game.UpdatedAt = now;
			await context.SaveChangesAsync(cancellationToken);
		}
		else
		{
			bettingRound.CurrentActorIndex = nextPlayerIndex;
			game.CurrentPlayerIndex = nextPlayerIndex;
			nextPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == nextPlayerIndex)?.Player.Name;

			game.UpdatedAt = now;
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
				ActionType = actionType,
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

	#region Validation

	private static string? ValidateAction(BettingActionType actionType, int amount, GamePlayer player,
		BettingRound bettingRound)
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
			BettingActionType.Raise => BettingActionMath.ValidateRaise(
				amount,
				bettingRound.CurrentBet,
				bettingRound.LastRaiseAmount,
				player.CurrentBet,
				player.ChipStack),
			BettingActionType.Fold when bettingRound.CurrentBet == player.CurrentBet =>
				"Cannot fold when you can check",
			BettingActionType.AllIn when player.ChipStack == 0 =>
				"Cannot go all-in - no chips remaining",
			_ => null
		};
	}

	#endregion

	private static BettingActionType NormalizeActionType(
		BettingActionType requestedAction,
		Game game,
		GamePlayer player,
		BettingRound bettingRound)
	{
		if (requestedAction != BettingActionType.Check) return requestedAction;

		if (!IsIrishHoldEmGame(game) && !IsPhilsMomGame(game) && !IsCrazyPineappleGame(game)) return requestedAction;

		return bettingRound.CurrentBet > player.CurrentBet
			? BettingActionType.Call
			: BettingActionType.Check;
	}

	#region Action Execution

	private static (int actualAmount, int chipsMoved) ExecuteAction(
		GamePlayer player,
		BettingActionType actionType,
		int amount,
		BettingRound bettingRound)
	{
		var actualAmount = 0;
		var chipsMoved = 0;

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
				var streetBet = bettingRound.CurrentBet;
				var (raiseTotal, raiseChips, raiseIncrement) = BettingActionMath.ComputeRaise(
					amount,
					streetBet,
					player.CurrentBet,
					player.ChipStack);
				actualAmount = raiseTotal;
				chipsMoved = raiseChips;
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

	#endregion

	#region Player Position Helpers

	private static int FindNextActivePlayer(List<GamePlayer> activePlayers, int currentIndex)
	{
		var totalPlayers = activePlayers.Max(p => p.SeatPosition) + 1;
		var searchIndex = (currentIndex + 1) % totalPlayers;

		for (var i = 0; i < totalPlayers; i++)
		{
			var player = activePlayers.FirstOrDefault(p => p.SeatPosition == searchIndex);
			if (player is not null && !player.HasFolded && !player.IsAllIn) return searchIndex;

			searchIndex = (searchIndex + 1) % totalPlayers;
		}

		return -1;
	}

	/// <summary>
	///     Finds the first active player to the left of the dealer — used for post-flop action.
	/// </summary>
	private static int FindFirstActivePlayerAfterDealer(Game game, List<GamePlayer> activePlayers)
	{
		var totalPlayers = activePlayers.Max(p => p.SeatPosition) + 1;
		var searchIndex = (game.DealerPosition + 1) % totalPlayers;

		for (var i = 0; i < totalPlayers; i++)
		{
			var player = activePlayers.FirstOrDefault(p => p.SeatPosition == searchIndex);
			if (player is not null && !player.HasFolded && !player.IsAllIn) return searchIndex;

			searchIndex = (searchIndex + 1) % totalPlayers;
		}

		return -1;
	}

	/// <summary>
	///     First seat still in the hand, left of the dealer. All-in seats still discard.
	/// </summary>
	private static int FindFirstPlayerInHandAfterDealer(Game game, List<GamePlayer> activePlayers)
	{
		var totalPlayers = activePlayers.Max(p => p.SeatPosition) + 1;
		var searchIndex = (game.DealerPosition + 1) % totalPlayers;

		for (var i = 0; i < totalPlayers; i++)
		{
			var player = activePlayers.FirstOrDefault(p => p.SeatPosition == searchIndex);
			if (player is not null && !player.HasFolded) return searchIndex;

			searchIndex = (searchIndex + 1) % totalPlayers;
		}

		return -1;
	}

	#endregion

	#region Round Completion

	private static bool AreAllPlayersAllIn(List<GamePlayer> activePlayers)
	{
		var playersInHand = activePlayers.Count(p => !p.HasFolded);

		// If only one player remains, not an all-in scenario
		if (playersInHand <= 1) return false;

		// Count players who can still bet
		var playersWhoCanBet = activePlayers.Count(p => !p.HasFolded && !p.IsAllIn && p.ChipStack > 0);

		return playersWhoCanBet < 2;
	}

	#endregion

	#region Phase Advancement

	private void AdvanceToNextPhase(Game game, List<GamePlayer> activePlayers)
	{
		// Check if only one player remains (all others folded)
		var playersInHand = activePlayers.Count(gp => !gp.HasFolded);
		if (playersInHand <= 1)
		{
			game.CurrentPhase = "Showdown";
			game.CurrentPlayerIndex = -1;
			return;
		}

		// Reset current bets for next round
		foreach (var gamePlayer in activePlayers) gamePlayer.CurrentBet = 0;

		// Hold 'Em phase progression:
		// PreFlop → Flop (deal 3 community cards, then betting)
		// Flop → Turn (deal 1 community card, then betting)
		// Turn → River (deal 1 community card, then betting)
		// River → Showdown
		switch (game.CurrentPhase)
		{
			case "PreFlop":
				game.CurrentPhase = IsPhilsMomGame(game) ? "DrawPhase" : "Flop";
				break;

			case "Flop":
				// Irish Hold 'Em and Phil's Mom insert a discard phase between Flop and Turn.
				if (IsIrishHoldEmGame(game) || IsPhilsMomGame(game))
					game.CurrentPhase = "DrawPhase";
				else
					game.CurrentPhase = "Turn";

				break;

			case "Turn":
				game.CurrentPhase = IsSouthDakotaGame(game) ? "Showdown" : "River";
				if (game.CurrentPhase == "Showdown") game.CurrentPlayerIndex = -1;

				break;

			case "River":
				game.CurrentPhase = IsKlondikeGame(game) ? "KlondikeReveal" : "Showdown";

				game.CurrentPlayerIndex = -1;
				break;
		}
	}

	private static bool IsIrishHoldEmGame(Game game)
	{
		return string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.IrishHoldEmCode,
			StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsPhilsMomGame(Game game)
	{
		return string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.PhilsMomCode,
			StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsCrazyPineappleGame(Game game)
	{
		return string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.CrazyPineappleCode,
			StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsRedRiverGame(Game game)
	{
		return string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.RedRiverCode,
			StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsSouthDakotaGame(Game game)
	{
		return string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.SouthDakotaCode,
			StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsKlondikeGame(Game game)
	{
		return string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.KlondikeCode,
			StringComparison.OrdinalIgnoreCase);
	}

	#endregion

	#region Community Card Dealing

	/// <summary>
	///     Irish, Phil's Mom, and Crazy Pineapple still owe a discard when fewer than two players can bet.
	///     Returns true when DrawPhase is set, false when the caller should run the board out, or an error to stop.
	/// </summary>
	private async Task<OneOf<bool, ProcessBettingActionError>> TryEnterOwedDiscardPhaseAsync(
		Game game,
		List<GamePlayer> activePlayers,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var gameTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code;
		var isIrish = string.Equals(gameTypeCode, PokerGameMetadataRegistry.IrishHoldEmCode,
			StringComparison.OrdinalIgnoreCase);
		var isPhilsMom = string.Equals(gameTypeCode, PokerGameMetadataRegistry.PhilsMomCode,
			StringComparison.OrdinalIgnoreCase);
		var isCrazyPineapple = string.Equals(gameTypeCode, PokerGameMetadataRegistry.CrazyPineappleCode,
			StringComparison.OrdinalIgnoreCase);
		if (!isIrish && !isPhilsMom && !isCrazyPineapple)
			return false;

		var holeCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Hand
			             && gc.GamePlayerId != null
			             && !gc.IsDiscarded)
			.Select(gc => new { gc.GamePlayerId, gc.Id })
			.ToListAsync(cancellationToken);

		var countByPlayer = holeCards
			.GroupBy(card => card.GamePlayerId!.Value)
			.ToDictionary(group => group.Key, group => group.Count());

		var seatCounts = activePlayers
			.Where(player => !player.HasFolded)
			.Select(player => (Seat: player.SeatPosition, Count: countByPlayer.GetValueOrDefault(player.Id)))
			.OrderBy(seat => seat.Seat)
			.ToList();

		if (seatCounts.Count == 0 || seatCounts.Select(seat => seat.Count).Distinct().Count() != 1)
			return InvalidHoleCardState(gameTypeCode, seatCounts);

		var holeCount = seatCounts[0].Count;
		if (holeCount == 2)
			return false;

		var owesDiscard = (isIrish && holeCount == 4)
		                  || (isCrazyPineapple && holeCount == 3)
		                  || (isPhilsMom && holeCount is 4 or 3);
		if (!owesDiscard)
			return InvalidHoleCardState(gameTypeCode, seatCounts);

		var communityCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Community)
			.ToListAsync(cancellationToken);
		var flopCount = communityCards.Count(card => card.DealtAtPhase == "Flop");

		if (isPhilsMom && holeCount == 4)
		{
			if (communityCards.Count != 0)
				return InvalidCommunityState(gameTypeCode, communityCards.Count, flopCount);
		}
		else if (flopCount == 0)
		{
			if (communityCards.Count != 0)
				return InvalidCommunityState(gameTypeCode, communityCards.Count, flopCount);

			await DealCommunityCardsForPhaseAsync(game, "Flop", now, cancellationToken);
		}
		else if (flopCount != 3 || communityCards.Count != 3)
		{
			return InvalidCommunityState(gameTypeCode, communityCards.Count, flopCount);
		}

		foreach (var player in activePlayers)
			player.HasDrawnThisRound = false;

		var firstDrawPlayerIndex = FindFirstPlayerInHandAfterDealer(game, activePlayers);
		if (firstDrawPlayerIndex < 0)
			return new ProcessBettingActionError
			{
				Code = ProcessBettingActionErrorCode.InvalidGameState,
				Message = $"Cannot enter discard for '{gameTypeCode}': no player remains in the hand."
			};

		game.CurrentPhase = "DrawPhase";
		game.CurrentDrawPlayerIndex = firstDrawPlayerIndex;
		game.CurrentPlayerIndex = firstDrawPlayerIndex;
		return true;
	}

	private static ProcessBettingActionError InvalidHoleCardState(
		string? gameTypeCode,
		IReadOnlyList<(int Seat, int Count)> seatCounts)
	{
		var details = seatCounts.Count == 0
			? "no non-folded seats"
			: string.Join(", ", seatCounts.Select(seat => $"seat {seat.Seat} has {seat.Count}"));

		return new ProcessBettingActionError
		{
			Code = ProcessBettingActionErrorCode.InvalidGameState,
			Message = $"Invalid hole-card counts for '{gameTypeCode}': {details}."
		};
	}

	private static ProcessBettingActionError InvalidCommunityState(
		string? gameTypeCode,
		int communityCount,
		int flopCount)
	{
		return new ProcessBettingActionError
		{
			Code = ProcessBettingActionErrorCode.InvalidGameState,
			Message =
				$"Invalid community cards for '{gameTypeCode}' before discard: {communityCount} community, {flopCount} flop."
		};
	}

	/// <summary>
	///     Deals community cards for the specified phase (Flop = 3 cards, Turn = 1 card, River = 1 card).
	/// </summary>
	private async Task DealCommunityCardsForPhaseAsync(
		Game game,
		string phase,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var isSouthDakota = IsSouthDakotaGame(game);
		var (cardCount, dealPhase, nextDealOrder) = phase switch
		{
			"Flop" => (isSouthDakota ? 2 : 3, "Flop", 1), // Flop: South Dakota=2, default=3
			"Turn" => (1, "Turn", isSouthDakota ? 3 : 4),
			"River" => (1, "River", 5),
			"RedRiverBonus" => (1, "RedRiverBonus", 6), // Red River bonus board card
			_ => (0, "", 0)
		};

		if (cardCount == 0) return;

		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		// Re-filter with tracked in-memory state. EF Core identity resolution can
		// return entities whose database row matched the WHERE clause even though
		// the tracked instance has already been moved out of the deck (e.g. the
		// River card dealt moments earlier in the same unit of work).
		deckCards = deckCards.Where(gc => gc.Location == CardLocation.Deck).ToList();

		for (var i = 0; i < cardCount && i < deckCards.Count; i++)
		{
			var card = deckCards[i];
			card.Location = CardLocation.Community;
			card.GamePlayerId = null;
			card.IsVisible = true;
			card.DealtAtPhase = dealPhase;
			card.DealOrder = nextDealOrder + i;
			card.DealtAt = now;
		}

		logger.LogInformation(
			"Dealt {CardCount} community card(s) for {Phase} in game {GameId}",
			cardCount, dealPhase, game.Id);
	}

	/// <summary>
	///     Deals all remaining community cards when all players are all-in (runout to showdown).
	/// </summary>
	private async Task DealRemainingCommunityCardsAsync(
		Game game,
		string currentPhase,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// Determine which community card phases still need to be dealt
		var isSouthDakota = IsSouthDakotaGame(game);
		var phaseOrder = isSouthDakota
			? new[] { "Flop", "Turn" }
			: new[] { "Flop", "Turn", "River" };
		var currentPhaseIndex = currentPhase switch
		{
			"PreFlop" => -1,
			"Flop" => 0,
			"Turn" => 1,
			"River" => isSouthDakota ? 3 : 2,
			_ => phaseOrder.Length // Already past all phases
		};

		// Load remaining deck cards
		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		var deckIndex = 0;
		var dealtPhases = new List<string>();

		// Deal community cards for each remaining phase
		for (var phaseIdx = currentPhaseIndex + 1; phaseIdx < phaseOrder.Length; phaseIdx++)
		{
			var phase = phaseOrder[phaseIdx];
			var (cardCount, _, nextDealOrder) = phase switch
			{
				"Flop" => (isSouthDakota ? 2 : 3, "Flop", 1),
				"Turn" => (1, "Turn", isSouthDakota ? 3 : 4),
				"River" => (1, "River", 5),
				_ => (0, "", 0)
			};

			dealtPhases.Add(phase);

			for (var i = 0; i < cardCount; i++)
			{
				if (deckIndex >= deckCards.Count)
				{
					logger.LogWarning(
						"Not enough cards in deck to deal remaining community cards for game {GameId}",
						game.Id);
					break;
				}

				var card = deckCards[deckIndex++];
				card.Location = CardLocation.Community;
				card.GamePlayerId = null;
				card.IsVisible = true;
				card.DealtAtPhase = phase;
				card.DealOrder = nextDealOrder + i;
				card.DealtAt = now;
			}
		}

		if (IsRedRiverGame(game) && await DealRedRiverBonusCommunityCardIfNeededAsync(game, now, cancellationToken))
			dealtPhases.Add("RedRiverBonus");

		// Store runout information in GameSettings for client-side animation
		var existingSettings = string.IsNullOrEmpty(game.GameSettings)
			? new Dictionary<string, object>()
			: JsonSerializer.Deserialize<Dictionary<string, object>>(game.GameSettings) ??
			  new Dictionary<string, object>();

		existingSettings["allInRunout"] = true;
		existingSettings["runoutStreets"] = dealtPhases;
		existingSettings["runoutHandNumber"] = game.CurrentHandNumber;
		existingSettings["runoutTimestamp"] = now.ToString("O");

		game.GameSettings = JsonSerializer.Serialize(existingSettings);

		// Move to Showdown (or KlondikeReveal for Klondike games)
		if (IsKlondikeGame(game))
		{
			game.CurrentPhase = "KlondikeReveal";
			await RevealKlondikeCardAsync(game, now, cancellationToken);
			game.DrawCompletedAt = now;
		}
		else
		{
			game.CurrentPhase = "Showdown";
		}

		game.CurrentPlayerIndex = -1;
		game.UpdatedAt = now;

		logger.LogInformation(
			"All remaining community cards dealt ({Phases}). Moving to {Phase} for game {GameId}",
			string.Join(", ", dealtPhases), game.CurrentPhase, game.Id);
	}

	private async Task<bool> DealRedRiverBonusCommunityCardIfNeededAsync(
		Game game,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// Prefer tracked state so a just-dealt, not-yet-saved River card can still
		// trigger the immediate Red River bonus flow before River betting starts.
		var riverCard = context.GameCards.Local
			.Where(gc => gc.GameId == game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Community
			             && !gc.IsDiscarded
			             && gc.DealtAtPhase == "River"
			             && context.Entry(gc).State is EntityState.Added or EntityState.Modified)
			.OrderByDescending(gc => gc.DealOrder)
			.FirstOrDefault();

		if (riverCard is null)
			// Fall back to store state for flows where River was dealt/suit-adjusted
			// in a different DbContext instance.
			riverCard = await context.GameCards
				.AsNoTracking()
				.Where(gc => gc.GameId == game.Id
				             && gc.HandNumber == game.CurrentHandNumber
				             && gc.Location == CardLocation.Community
				             && !gc.IsDiscarded
				             && gc.DealtAtPhase == "River")
				.OrderByDescending(gc => gc.DealOrder)
				.FirstOrDefaultAsync(cancellationToken);

		if (riverCard is null) return false;

		if (riverCard.Suit is not CardSuit.Hearts and not CardSuit.Diamonds) return false;

		var hasBonusCard = context.GameCards.Local.Any(gc =>
			gc.GameId == game.Id
			&& gc.HandNumber == game.CurrentHandNumber
			&& gc.Location == CardLocation.Community
			&& !gc.IsDiscarded
			&& gc.DealtAtPhase == "RedRiverBonus"
			&& context.Entry(gc).State != EntityState.Deleted);

		if (!hasBonusCard)
			hasBonusCard = await context.GameCards
				.AsNoTracking()
				.AnyAsync(
					gc => gc.GameId == game.Id
					      && gc.HandNumber == game.CurrentHandNumber
					      && gc.Location == CardLocation.Community
					      && !gc.IsDiscarded
					      && gc.DealtAtPhase == "RedRiverBonus",
					cancellationToken);

		if (hasBonusCard) return false;

		await DealCommunityCardsForPhaseAsync(game, "RedRiverBonus", now, cancellationToken);
		return true;
	}

	/// <summary>
	///     Reveals the face-down Klondike wild card so all players can see it
	///     during the KlondikeReveal phase before showdown begins.
	/// </summary>
	private async Task RevealKlondikeCardAsync(
		Game game,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var klondikeCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.DealtAtPhase == "KlondikeCard"
			             && !gc.IsVisible)
			.ToListAsync(cancellationToken);

		foreach (var card in klondikeCards) card.IsVisible = true;

		logger.LogInformation(
			"Revealed {Count} Klondike card(s) for game {GameId} at KlondikeReveal phase",
			klondikeCards.Count, game.Id);
	}

	#endregion
}