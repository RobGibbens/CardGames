using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Text.Json;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using BettingRound = CardGames.Poker.Api.Data.Entities.BettingRound;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBettingAction;

public class ProcessBettingActionCommandHandler(
	CardsDbContext context,
	IMediator mediator,
	ILogger<ProcessBettingActionCommandHandler> logger)
	: IRequestHandler<ProcessBettingActionCommand, OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>>
{
	public async Task<OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>> Handle(
		ProcessBettingActionCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

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

		var bettingPhases = new[]
		{
			nameof(Phases.ThirdStreet),
			nameof(Phases.FourthStreet),
			nameof(Phases.FifthStreet),
			nameof(Phases.SixthStreet),
			nameof(Phases.SeventhStreet)
		};

		if (!bettingPhases.Contains(game.CurrentPhase))
		{
			return new ProcessBettingActionError
			{
				Message = $"Cannot process betting action. Game is in '{game.CurrentPhase}' phase.",
				Code = ProcessBettingActionErrorCode.InvalidGameState
			};
		}

		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		var bettingRound = await context.BettingRounds
			.Include(br => br.Actions)
			.FirstOrDefaultAsync(br => br.GameId == command.GameId &&
										br.HandNumber == game.CurrentHandNumber &&
										!br.IsComplete,
				cancellationToken);

		if (bettingRound is null)
		{
			return new ProcessBettingActionError
			{
				Message = "No active betting round found.",
				Code = ProcessBettingActionErrorCode.NoBettingRound
			};
		}

		var currentPlayer = activePlayers.FirstOrDefault(p => p.SeatPosition == bettingRound.CurrentActorIndex);
		if (currentPlayer is null)
		{
			return new ProcessBettingActionError
			{
				Message = "Current player not found.",
				Code = ProcessBettingActionErrorCode.InvalidGameState
			};
		}

		if (currentPlayer.SeatPosition != game.CurrentPlayerIndex)
		{
			return new ProcessBettingActionError
			{
				Message = "It is not this player's turn.",
				Code = ProcessBettingActionErrorCode.NotPlayerTurn
			};
		}

		if (currentPlayer.HasFolded)
		{
			return new ProcessBettingActionError
			{
				Message = "Player has already folded.",
				Code = ProcessBettingActionErrorCode.InvalidAction
			};
		}

		if (currentPlayer.IsAllIn)
		{
			return new ProcessBettingActionError
			{
				Message = "Player is already all-in and cannot take further actions.",
				Code = ProcessBettingActionErrorCode.InvalidAction
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
				logger.LogInformation(
					"All players are all-in after {Phase} for game {GameId}. Dealing remaining streets and proceeding to showdown.",
					game.CurrentPhase, game.Id);

				var previousPhase = game.CurrentPhase;
				AdvanceToNextPhase(game, activePlayers);

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

			var previousPhaseNormal = game.CurrentPhase;
			AdvanceToNextPhase(game, activePlayers);

			logger.LogInformation(
				"Betting round complete. Advancing from {PreviousPhase} to {NewPhase} for game {GameId}, hand {HandNumber}",
				previousPhaseNormal, game.CurrentPhase, game.Id, game.CurrentHandNumber);

			game.UpdatedAt = now;

			var streetPhases = new[]
			{
				nameof(Phases.FourthStreet),
				nameof(Phases.FifthStreet),
				nameof(Phases.SixthStreet),
				nameof(Phases.SeventhStreet)
			};

			if (streetPhases.Contains(game.CurrentPhase))
			{
				var executionStrategy = context.Database.CreateExecutionStrategy();
				OneOf<DealHandsSuccessful, DealHandsError>? dealResultHolder = null;

				await executionStrategy.ExecuteAsync(async ct =>
				{
					await using var transaction = await context.Database.BeginTransactionAsync(ct);
					await context.SaveChangesAsync(ct);

					dealResultHolder = await mediator.Send(new DealHandsCommand(game.Id), ct);

					if (dealResultHolder.Value.IsT0)
					{
						await transaction.CommitAsync(ct);
					}
					else
					{
						await transaction.RollbackAsync(ct);
					}
				}, cancellationToken);

				if (dealResultHolder?.IsT0 == true)
				{
					var dealSuccess = dealResultHolder.Value.AsT0;
					nextPlayerIndex = dealSuccess.CurrentPlayerIndex;
					nextPlayerName = dealSuccess.CurrentPlayerName;
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
			else
			{
				await context.SaveChangesAsync(cancellationToken);
			}
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

	private void AdvanceToNextPhase(Game game, List<GamePlayer> activePlayers)
	{
		var playersInHand = activePlayers.Count(gp => !gp.HasFolded);
		if (playersInHand <= 1)
		{
			game.CurrentPhase = nameof(Phases.Showdown);
			game.CurrentPlayerIndex = -1;
			return;
		}

		foreach (var gamePlayer in activePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

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

	private static bool AreAllPlayersAllIn(List<GamePlayer> activePlayers)
	{
		var playersInHand = activePlayers.Count(p => !p.HasFolded);
		if (playersInHand <= 1)
		{
			return false;
		}

		var playersWhoCanBet = activePlayers.Count(p => !p.HasFolded && !p.IsAllIn && p.ChipStack > 0);
		return playersWhoCanBet < 2;
	}

	private async Task DealRemainingStreetsAsync(
		Game game,
		List<GamePlayer> activePlayers,
		string currentPhase,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var streetOrder = new[]
		{
			nameof(Phases.FourthStreet),
			nameof(Phases.FifthStreet),
			nameof(Phases.SixthStreet),
			nameof(Phases.SeventhStreet)
		};

		var startIndex = Array.IndexOf(streetOrder, currentPhase);
		if (startIndex < 0)
		{
			return;
		}

		var playersToReceiveCards = activePlayers.Where(p => !p.HasFolded).OrderBy(p => p.SeatPosition).ToList();

		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
						 gc.HandNumber == game.CurrentHandNumber &&
						 gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		var deckIndex = 0;

		var dealtStreets = new List<string>();

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

			dealtStreets.Add(street);
		}

		var existingSettings = string.IsNullOrEmpty(game.GameSettings)
			? new Dictionary<string, object>()
			: JsonSerializer.Deserialize<Dictionary<string, object>>(game.GameSettings) ?? new Dictionary<string, object>();

		existingSettings["allInRunout"] = true;
		existingSettings["runoutStreets"] = dealtStreets;
		existingSettings["runoutHandNumber"] = game.CurrentHandNumber;
		existingSettings["runoutTimestamp"] = now.ToString("O");

		game.GameSettings = JsonSerializer.Serialize(existingSettings);

		game.CurrentPhase = nameof(Phases.Showdown);
		game.CurrentPlayerIndex = -1;

		await context.SaveChangesAsync(cancellationToken);
	}
}
