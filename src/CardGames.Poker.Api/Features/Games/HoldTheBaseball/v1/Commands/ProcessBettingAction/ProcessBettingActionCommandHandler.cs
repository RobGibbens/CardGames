using System.Text.Json;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using BettingRound = CardGames.Poker.Api.Data.Entities.BettingRound;

using CardGames.Poker.Api.Services.InMemoryEngine;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.ProcessBettingAction;

/// <summary>
/// Handles the <see cref="ProcessBettingActionCommand"/> to process betting actions
/// in a Hold the Baseball game. Identical flow to Hold 'Em.
/// </summary>
public class ProcessBettingActionCommandHandler(
	CardsDbContext context,
	IOptions<InMemoryEngineOptions> engineOptions,
	IGameStateManager gameStateManager,
	ILogger<ProcessBettingActionCommandHandler> logger)
	: IRequestHandler<ProcessBettingActionCommand, OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>>
{
	private static readonly string[] ValidBettingPhases = ["PreFlop", "Flop", "Turn", "River"];

	public async Task<OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>> Handle(
		ProcessBettingActionCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameType)
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

		if (!ValidBettingPhases.Contains(game.CurrentPhase))
		{
			return new ProcessBettingActionError
			{
				Message = $"Cannot process betting action. Game is in '{game.CurrentPhase}' phase. " +
						  "Betting is only allowed during PreFlop, Flop, Turn, or River phases.",
				Code = ProcessBettingActionErrorCode.InvalidGameState
			};
		}

		var bettingRounds = await context.BettingRounds
			.Where(br => br.GameId == game.Id &&
						 br.HandNumber == game.CurrentHandNumber &&
						 !br.IsComplete)
			.Include(br => br.Actions)
			.OrderByDescending(br => br.RoundNumber)
			.ToListAsync(cancellationToken);

		var bettingRound = bettingRounds.FirstOrDefault();
		if (bettingRound is null)
		{
			return new ProcessBettingActionError
			{
				Message = $"No active betting round found for hand {game.CurrentHandNumber} in phase '{game.CurrentPhase}'.",
				Code = ProcessBettingActionErrorCode.NoBettingRound
			};
		}

		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		var currentPlayer = activePlayers.FirstOrDefault(gp => gp.SeatPosition == bettingRound.CurrentActorIndex);
		if (currentPlayer is null)
		{
			return new ProcessBettingActionError
			{
				Message = "Could not determine current player.",
				Code = ProcessBettingActionErrorCode.InvalidGameState
			};
		}

		var validationResult = ValidateAction(command.ActionType, command.Amount, currentPlayer, bettingRound);
		if (validationResult is not null)
		{
			return new ProcessBettingActionError
			{
				Message = validationResult,
				Code = ProcessBettingActionErrorCode.InvalidAction
			};
		}

		var chipStackBefore = currentPlayer.ChipStack;
		var potBefore = game.Pots.Sum(p => p.Amount);

		var (actualAmount, chipsMoved) = ExecuteAction(currentPlayer, command.ActionType, command.Amount, bettingRound);

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

		var mainPot = game.Pots.FirstOrDefault(p => p.PotOrder == 0 && p.HandNumber == game.CurrentHandNumber);
		if (mainPot is not null)
		{
			mainPot.Amount += chipsMoved;
		}

		bettingRound.PlayersActed++;

		var roundComplete = false;
		var nextPlayerIndex = -1;
		string? nextPlayerName = null;

		nextPlayerIndex = FindNextActivePlayer(activePlayers, bettingRound.CurrentActorIndex);
		roundComplete = IsRoundComplete(activePlayers, bettingRound, nextPlayerIndex);

		if (roundComplete)
		{
			bettingRound.IsComplete = true;
			bettingRound.CompletedAt = now;
			nextPlayerIndex = -1;

			var allPlayersAllIn = AreAllPlayersAllIn(activePlayers);

			if (allPlayersAllIn)
			{
				await DealRemainingCommunityCardsAsync(game, game.CurrentPhase, now, cancellationToken);

				game.UpdatedAt = now;
				await context.SaveChangesAsync(cancellationToken);

				if (engineOptions.Value.Enabled)
					await gameStateManager.ReloadGameAsync(command.GameId, cancellationToken);

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

			AdvanceToNextPhase(game, activePlayers);

			if (game.CurrentPhase is "Flop" or "Turn" or "River")
			{
				await DealCommunityCardsForPhaseAsync(game, game.CurrentPhase, now, cancellationToken);

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
				nextPlayerIndex = -1;
				nextPlayerName = null;
				game.CurrentPlayerIndex = -1;
			}

			game.UpdatedAt = now;
			await context.SaveChangesAsync(cancellationToken);

			if (engineOptions.Value.Enabled)
				await gameStateManager.ReloadGameAsync(command.GameId, cancellationToken);
		}
		else
		{
			bettingRound.CurrentActorIndex = nextPlayerIndex;
			game.CurrentPlayerIndex = nextPlayerIndex;
			nextPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == nextPlayerIndex)?.Player.Name;

			game.UpdatedAt = now;
			await context.SaveChangesAsync(cancellationToken);

			if (engineOptions.Value.Enabled)
				await gameStateManager.ReloadGameAsync(command.GameId, cancellationToken);
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

	#region Validation

	private static string? ValidateAction(BettingActionType actionType, int amount, GamePlayer player, BettingRound bettingRound)
	{
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

	#endregion

	#region Action Execution

	private static (int actualAmount, int chipsMoved) ExecuteAction(
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
			if (player is not null && !player.HasFolded && !player.IsAllIn)
			{
				return searchIndex;
			}
			searchIndex = (searchIndex + 1) % totalPlayers;
		}

		return -1;
	}

	private static int FindFirstActivePlayerAfterDealer(Game game, List<GamePlayer> activePlayers)
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

	#endregion

	#region Round Completion

	private bool IsRoundComplete(List<GamePlayer> activePlayers, BettingRound bettingRound, int nextPlayerIndex)
	{
		var playersInHand = activePlayers.Count(p => !p.HasFolded);
		if (playersInHand <= 1) return true;

		var playersWhoCanAct = activePlayers.Where(p => !p.HasFolded && !p.IsAllIn).ToList();
		if (playersWhoCanAct.Count == 0) return true;

		var allMatched = playersWhoCanAct.All(p => p.CurrentBet == bettingRound.CurrentBet);

		if (bettingRound.LastAggressorIndex >= 0 && nextPlayerIndex == bettingRound.LastAggressorIndex && allMatched)
		{
			return true;
		}

		if (bettingRound.PlayersActed >= playersWhoCanAct.Count && allMatched)
		{
			return true;
		}

		return false;
	}

	private static bool AreAllPlayersAllIn(List<GamePlayer> activePlayers)
	{
		var playersInHand = activePlayers.Count(p => !p.HasFolded);
		if (playersInHand <= 1) return false;

		var playersWhoCanBet = activePlayers.Count(p => !p.HasFolded && !p.IsAllIn && p.ChipStack > 0);
		return playersWhoCanBet < 2;
	}

	#endregion

	#region Phase Advancement

	private void AdvanceToNextPhase(Game game, List<GamePlayer> activePlayers)
	{
		var playersInHand = activePlayers.Count(gp => !gp.HasFolded);
		if (playersInHand <= 1)
		{
			game.CurrentPhase = "Showdown";
			game.CurrentPlayerIndex = -1;
			return;
		}

		foreach (var gamePlayer in activePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		switch (game.CurrentPhase)
		{
			case "PreFlop":
				game.CurrentPhase = "Flop";
				break;
			case "Flop":
				game.CurrentPhase = "Turn";
				break;
			case "Turn":
				game.CurrentPhase = "River";
				break;
			case "River":
				game.CurrentPhase = "Showdown";
				game.CurrentPlayerIndex = -1;
				break;
		}
	}

	#endregion

	#region Community Card Dealing

	private async Task DealCommunityCardsForPhaseAsync(
		Game game,
		string phase,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var (cardCount, dealPhase, nextDealOrder) = phase switch
		{
			"Flop" => (3, "Flop", 1),
			"Turn" => (1, "Turn", 4),
			"River" => (1, "River", 5),
			_ => (0, "", 0)
		};

		if (cardCount == 0) return;

		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id
				&& gc.HandNumber == game.CurrentHandNumber
				&& gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		for (int i = 0; i < cardCount && i < deckCards.Count; i++)
		{
			var card = deckCards[i];
			card.Location = CardLocation.Community;
			card.GamePlayerId = null;
			card.IsVisible = true;
			card.DealtAtPhase = dealPhase;
			card.DealOrder = nextDealOrder + i;
			card.DealtAt = now;
		}
	}

	private async Task DealRemainingCommunityCardsAsync(
		Game game,
		string currentPhase,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var phaseOrder = new[] { "Flop", "Turn", "River" };
		var currentPhaseIndex = currentPhase switch
		{
			"PreFlop" => -1,
			"Flop" => 0,
			"Turn" => 1,
			"River" => 2,
			_ => 3
		};

		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id
				&& gc.HandNumber == game.CurrentHandNumber
				&& gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		var deckIndex = 0;
		var dealtPhases = new List<string>();

		for (var phaseIdx = currentPhaseIndex + 1; phaseIdx < phaseOrder.Length; phaseIdx++)
		{
			var phase = phaseOrder[phaseIdx];
			var (cardCount, _, nextDealOrder) = phase switch
			{
				"Flop" => (3, "Flop", 1),
				"Turn" => (1, "Turn", 4),
				"River" => (1, "River", 5),
				_ => (0, "", 0)
			};

			dealtPhases.Add(phase);

			for (int i = 0; i < cardCount; i++)
			{
				if (deckIndex >= deckCards.Count) break;

				var card = deckCards[deckIndex++];
				card.Location = CardLocation.Community;
				card.GamePlayerId = null;
				card.IsVisible = true;
				card.DealtAtPhase = phase;
				card.DealOrder = nextDealOrder + i;
				card.DealtAt = now;
			}
		}

		var existingSettings = string.IsNullOrEmpty(game.GameSettings)
			? new Dictionary<string, object>()
			: JsonSerializer.Deserialize<Dictionary<string, object>>(game.GameSettings) ?? new Dictionary<string, object>();

		existingSettings["allInRunout"] = true;
		existingSettings["runoutStreets"] = dealtPhases;
		existingSettings["runoutHandNumber"] = game.CurrentHandNumber;
		existingSettings["runoutTimestamp"] = now.ToString("O");

		game.GameSettings = JsonSerializer.Serialize(existingSettings);
		game.CurrentPhase = "Showdown";
		game.CurrentPlayerIndex = -1;
		game.UpdatedAt = now;
	}

	#endregion
}
