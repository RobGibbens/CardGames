using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.FiveCardDraw;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.ProcessBettingAction;

/// <summary>
/// Handles the <see cref="ProcessBettingActionCommand"/> to process betting actions from players.
/// </summary>
public class ProcessBettingActionCommandHandler(CardsDbContext context)
	: IRequestHandler<ProcessBettingActionCommand, OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>>
{
	/// <inheritdoc />
	public async Task<OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>> Handle(
		ProcessBettingActionCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players and active betting round
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.BettingRounds.Where(br => !br.IsComplete))
				.ThenInclude(br => br.Actions)
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

		// 2. Validate game is in a betting phase
		var validBettingPhases = new[]
		{
			nameof(FiveCardDrawPhase.FirstBettingRound),
			nameof(FiveCardDrawPhase.SecondBettingRound)
		};

		if (!validBettingPhases.Contains(game.CurrentPhase))
		{
			return new ProcessBettingActionError
			{
				Message = $"Cannot process betting action. Game is in '{game.CurrentPhase}' phase. " +
				          $"Betting is only allowed during '{nameof(FiveCardDrawPhase.FirstBettingRound)}' or '{nameof(FiveCardDrawPhase.SecondBettingRound)}' phases.",
				Code = ProcessBettingActionErrorCode.InvalidGameState
			};
		}

		// 3. Get the active betting round
		var bettingRound = game.BettingRounds.FirstOrDefault(br => !br.IsComplete);
		if (bettingRound is null)
		{
			return new ProcessBettingActionError
			{
				Message = "No active betting round found.",
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

			// Advance to next phase
			AdvanceToNextPhase(game, activePlayers);
		}
		else
		{
			bettingRound.CurrentActorIndex = nextPlayerIndex;
			game.CurrentPlayerIndex = nextPlayerIndex;
			nextPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == nextPlayerIndex)?.Player.Name;
		}

		// 13. Update timestamps
		game.UpdatedAt = now;

		// 14. Persist changes
		await context.SaveChangesAsync(cancellationToken);

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
			game.CurrentPhase = nameof(FiveCardDrawPhase.Showdown);
			game.CurrentPlayerIndex = -1;
			return;
		}

		// Reset current bets for next round
		foreach (var gamePlayer in activePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		switch (game.CurrentPhase)
		{
			case nameof(FiveCardDrawPhase.FirstBettingRound):
				game.CurrentPhase = nameof(FiveCardDrawPhase.DrawPhase);
				game.CurrentDrawPlayerIndex = FindFirstActivePlayerAfterDealer(game, activePlayers);
				game.CurrentPlayerIndex = game.CurrentDrawPlayerIndex;
				break;

			case nameof(FiveCardDrawPhase.SecondBettingRound):
				game.CurrentPhase = nameof(FiveCardDrawPhase.Showdown);
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
}

