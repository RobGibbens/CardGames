using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using static CardGames.Poker.Api.Features.Games.InBetween.InBetweenVariantState;

using CardGames.Poker.Api.Services.InMemoryEngine;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.PlaceBet;

/// <summary>
/// Handles the <see cref="PlaceBetCommand"/> to process a player's bet or pass in In-Between.
/// Deals boundary cards on first access, validates bet, deals third card, resolves outcome,
/// and advances to the next player or completes the game.
/// </summary>
public class PlaceBetCommandHandler(
	CardsDbContext context,
	IOptions<InMemoryEngineOptions> engineOptions,
	IGameStateManager gameStateManager,
	IGameFlowHandlerFactory flowHandlerFactory,
	IHandHistoryRecorder handHistoryRecorder)
	: IRequestHandler<PlaceBetCommand, OneOf<PlaceBetSuccessful, PlaceBetError>>
{
	public async Task<OneOf<PlaceBetSuccessful, PlaceBetError>> Handle(
		PlaceBetCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load game with all related data
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameCards)
			.Include(g => g.Pots)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new PlaceBetError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = PlaceBetErrorCode.GameNotFound
			};
		}

		// 2. Validate game is in InBetweenTurn phase
		if (!string.Equals(game.CurrentPhase, nameof(Phases.InBetweenTurn), StringComparison.OrdinalIgnoreCase))
		{
			return new PlaceBetError
			{
				Message = $"Cannot place bet. Game is in '{game.CurrentPhase}' phase.",
				Code = PlaceBetErrorCode.InvalidPhase
			};
		}

		// 3. Find the player
		var gamePlayer = game.GamePlayers.FirstOrDefault(gp => gp.PlayerId == command.PlayerId);
		if (gamePlayer is null)
		{
			return new PlaceBetError
			{
				Message = $"Player with ID '{command.PlayerId}' is not in this game.",
				Code = PlaceBetErrorCode.PlayerNotFound
			};
		}

		// 4. Validate it's this player's turn
		if (gamePlayer.SeatPosition != game.CurrentPlayerIndex)
		{
			return new PlaceBetError
			{
				Message = "It's not this player's turn.",
				Code = PlaceBetErrorCode.NotPlayersTurn
			};
		}

		var state = GetState(game);

		// 5. If boundary cards haven't been dealt yet, deal them now
		if (state.SubPhase == TurnSubPhase.AwaitingFirstBoundary)
		{
			await InBetweenFlowHandler.DealBoundaryCardsAsync(context, game, now, cancellationToken);
			state = GetState(game);

			// If ace choice is required, tell client to make that choice first
			if (state.SubPhase == TurnSubPhase.AwaitingAceChoice)
			{
				return new PlaceBetError
				{
					Message = "First boundary card is an Ace. Player must declare high or low before betting.",
					Code = PlaceBetErrorCode.AceChoiceRequired
				};
			}

			// Boundary cards dealt — return early so the player can view cards and decide
			var pot = game.Pots.FirstOrDefault(p => p.PotType == PotType.Main
			                                        && p.HandNumber == game.CurrentHandNumber);
			return new PlaceBetSuccessful
			{
				GameId = game.Id,
				PlayerId = command.PlayerId,
				Amount = 0,
				TurnResult = "BoundaryCardsDealt",
				Description = "Boundary cards dealt.",
				PotAmount = pot?.Amount ?? 0
			};
		}

		// 6. Ensure we're in the right sub-phase for betting
		if (state.SubPhase == TurnSubPhase.AwaitingAceChoice)
		{
			return new PlaceBetError
			{
				Message = "Ace choice must be made before placing a bet.",
				Code = PlaceBetErrorCode.AceChoiceRequired
			};
		}

		if (state.SubPhase != TurnSubPhase.AwaitingBetOrPass)
		{
			return new PlaceBetError
			{
				Message = $"Cannot place bet in current sub-phase: {state.SubPhase}.",
				Code = PlaceBetErrorCode.InvalidPhase
			};
		}

		// 7. Validate bet amount
		var mainPot = game.Pots.FirstOrDefault(p => p.PotType == PotType.Main
		                                            && p.HandNumber == game.CurrentHandNumber);
		var potAmount = mainPot?.Amount ?? 0;
		var betAmount = command.Amount;

		// Zero-chip players forced to pass
		if (gamePlayer.ChipStack <= 0 && betAmount > 0)
		{
			return new PlaceBetError
			{
				Message = "Player has no chips and must pass.",
				Code = PlaceBetErrorCode.InvalidBetAmount
			};
		}

		if (betAmount < 0)
		{
			return new PlaceBetError
			{
				Message = "Bet amount cannot be negative.",
				Code = PlaceBetErrorCode.InvalidBetAmount
			};
		}

		if (betAmount > potAmount)
		{
			return new PlaceBetError
			{
				Message = $"Bet amount ({betAmount}) exceeds the pot ({potAmount}).",
				Code = PlaceBetErrorCode.BetExceedsPot
			};
		}

		if (betAmount > gamePlayer.ChipStack)
		{
			return new PlaceBetError
			{
				Message = $"Bet amount ({betAmount}) exceeds player's chip stack ({gamePlayer.ChipStack}).",
				Code = PlaceBetErrorCode.BetExceedsChips
			};
		}

		// First-orbit restriction: cannot bet the full pot
		var isFirstOrbit = !InBetweenFlowHandler.AllPlayersCompletedFirstTurn(game, state);
		if (isFirstOrbit && betAmount > 0 && betAmount == potAmount)
		{
			return new PlaceBetError
			{
				Message = "Full-pot bets are not allowed during the first orbit.",
				Code = PlaceBetErrorCode.FullPotNotAllowedFirstOrbit
			};
		}

		// 8. Process the bet
		string turnResultStr;
		string? description;

		if (betAmount == 0)
		{
			// Pass — mark first turn completed, advance
			state.LastTurnResult = TurnResult.Pass;
			state.LastTurnDescription = $"{gamePlayer.Player?.Name ?? "Unknown"} passes";
			state.SubPhase = TurnSubPhase.TurnComplete;
			state.PlayersCompletedFirstTurn.Add(gamePlayer.SeatPosition);
			SetState(game, state);
			game.UpdatedAt = now;
			await context.SaveChangesAsync(cancellationToken);

			if (engineOptions.Value.Enabled)
				await gameStateManager.ReloadGameAsync(command.GameId, cancellationToken);

			turnResultStr = "Pass";
			description = state.LastTurnDescription;
		}
		else
		{
			// Resolve the bet — deal third card and determine outcome
			var result = await InBetweenFlowHandler.ResolveTurnAsync(
				context, game, betAmount, now, cancellationToken);

			state = GetState(game);
			turnResultStr = result.ToString();
			description = state.LastTurnDescription;

			// Mark the reveal timestamp so the background service advances
			// after the card-reveal display period (5 seconds).
			game.DrawCompletedAt = now;
			await context.SaveChangesAsync(cancellationToken);

			if (engineOptions.Value.Enabled)
				await gameStateManager.ReloadGameAsync(command.GameId, cancellationToken);
		}

		// 9. Advance to next player or complete game (only for pass; bets wait for reveal display)
		if (betAmount == 0)
		{
			await InBetweenFlowHandler.AdvanceToNextPlayerOrCompleteAsync(
				context, game, handHistoryRecorder, now, cancellationToken);
		}

		// Re-read pot amount after resolution
		var finalPot = await context.Pots
			.Where(p => p.GameId == game.Id &&
			            p.HandNumber == game.CurrentHandNumber &&
			            p.PotType == PotType.Main)
			.Select(p => p.Amount)
			.FirstOrDefaultAsync(cancellationToken);

		return new PlaceBetSuccessful
		{
			GameId = game.Id,
			PlayerId = command.PlayerId,
			Amount = betAmount,
			TurnResult = turnResultStr,
			Description = description,
			NextPhase = game.CurrentPhase,
			NextPlayerSeatIndex = game.CurrentPlayerIndex >= 0 ? game.CurrentPlayerIndex : null,
			PotAmount = finalPot
		};
	}
}
