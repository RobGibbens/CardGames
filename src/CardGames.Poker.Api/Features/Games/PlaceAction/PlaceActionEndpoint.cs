using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using CardGames.Poker.Betting;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace CardGames.Poker.Api.Features.Games.PlaceAction;

public static class PlaceActionEndpoint
{
	[WolverinePost("/api/v1/games/{gameId}/hands/current/actions")]
	[EndpointName("PlaceAction")]
	public static async Task<Results<Ok<PlaceActionResponse>, NotFound<string>, BadRequest<string>>> Post(
		Guid gameId,
		PlaceActionRequest request,
		IDocumentSession session,
		CancellationToken cancellationToken)
	{
		// Load the current game state
		var game = await session.Events.AggregateStreamAsync<PokerGameAggregate>(
			gameId,
			token: cancellationToken);

		if (game == null)
		{
			return TypedResults.NotFound($"Game with ID {gameId} not found.");
		}

		// Validate it's this player's turn
		if (!game.IsPlayerTurn(request.PlayerId))
		{
			return TypedResults.BadRequest("It is not this player's turn to act.");
		}

		// Validate action is available
		var availableActions = game.GetAvailableActions();
		var validationError = ValidateAction(request, availableActions);
		if (validationError != null)
		{
			return TypedResults.BadRequest(validationError);
		}

		// Process the action through domain logic
		var result = game.ProcessBettingAction(request.PlayerId, request.ActionType, request.Amount);

		if (!result.Success)
		{
			return TypedResults.BadRequest(result.ErrorMessage ?? "Failed to process action.");
		}

		// Create and append the event
		var actionEvent = new BettingActionPerformed(
			gameId,
			game.CurrentHandId!.Value,
			request.PlayerId,
			request.ActionType,
			result.ActualAmount,
			game.TotalPot,
			result.PlayerChipStack,
			result.RoundComplete,
			result.NewPhase,
			DateTime.UtcNow
		);

		session.Events.Append(gameId, actionEvent);
		await session.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(new PlaceActionResponse(
			Success: true,
			ActionDescription: result.ActionDescription,
			NewPot: game.TotalPot,
			NextPlayerToAct: game.CurrentPlayerToAct,
			RoundComplete: result.RoundComplete,
			PhaseAdvanced: result.PhaseAdvanced,
			CurrentPhase: game.CurrentPhase.ToString()
		));
	}

	private static string? ValidateAction(PlaceActionRequest request, AvailableActions available)
	{
		return request.ActionType switch
		{
			BettingActionType.Check when !available.CanCheck =>
				"Cannot check - there is a bet to match.",
			BettingActionType.Bet when !available.CanBet =>
				"Cannot bet - betting is not available.",
			BettingActionType.Bet when request.Amount < available.MinBet =>
				$"Bet must be at least {available.MinBet}.",
			BettingActionType.Bet when request.Amount > available.MaxBet =>
				$"Cannot bet more than your stack ({available.MaxBet}).",
			BettingActionType.Call when !available.CanCall =>
				"Cannot call - no bet to match.",
			BettingActionType.Raise when !available.CanRaise =>
				"Cannot raise - raising is not available.",
			BettingActionType.Raise when request.Amount < available.MinRaise =>
				$"Raise must be at least {available.MinRaise}.",
			BettingActionType.Fold when !available.CanFold && available.CanCheck =>
				"Cannot fold when you can check.",
			BettingActionType.AllIn when !available.CanAllIn =>
				"Cannot go all-in - no chips remaining.",
			_ => null
		};
	}
}
