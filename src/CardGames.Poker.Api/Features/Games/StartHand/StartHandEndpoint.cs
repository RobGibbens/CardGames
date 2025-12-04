using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace CardGames.Poker.Api.Features.Games.StartHand;

public static class StartHandEndpoint
{
	[WolverinePost("/api/v1/games/{gameId}/hands")]
	[EndpointName("StartHand")]
	public static async Task<Results<Ok<StartHandResponse>, NotFound<string>, BadRequest<string>>> Post(
		Guid gameId,
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

		// Validate game can start a hand
		if (!game.CanStartHand())
		{
			return TypedResults.BadRequest("Cannot start hand - game is not ready or already has active hand.");
		}

		var handId = Guid.NewGuid();
		var handNumber = game.HandNumber + 1;
		var dealerPosition = game.GetNextDealerPosition();

		// Create the event
		var handStartedEvent = new HandStarted(
			gameId,
			handId,
			handNumber,
			dealerPosition,
			DateTime.UtcNow
		);

		session.Events.Append(gameId, handStartedEvent);
		await session.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(new StartHandResponse(
			handId,
			handNumber,
			HandPhase.CollectingAntes.ToString(),
			dealerPosition,
			Pot: 0,
			NextPlayerToAct: null
		));
	}
}