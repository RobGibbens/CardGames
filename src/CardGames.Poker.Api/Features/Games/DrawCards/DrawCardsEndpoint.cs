using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace CardGames.Poker.Api.Features.Games.DrawCards;

/// <summary>
/// Endpoint for drawing cards (discard and replace) in the draw phase.
/// </summary>
public static class DrawCardsEndpoint
{
	[WolverinePost("/api/v1/games/{gameId}/hands/current/draw")]
	[EndpointName("DrawCards")]
	public static async Task<Results<Ok<DrawCardsResponse>, NotFound<string>, BadRequest<string>>> Post(
		Guid gameId,
		DrawCardsRequest request,
		IDocumentSession session,
		CancellationToken cancellationToken)
	{
		var game = await session.Events.AggregateStreamAsync<PokerGameAggregate>(
			gameId,
			token: cancellationToken);

		if (game == null)
		{
			return TypedResults.NotFound($"Game with ID {gameId} not found.");
		}

		if (game.CurrentHandId == null)
		{
			return TypedResults.BadRequest("No active hand in this game.");
		}

		if (game.CurrentPhase != HandPhase.DrawPhase)
		{
			return TypedResults.BadRequest($"Cannot draw cards in phase {game.CurrentPhase}. Must be in DrawPhase.");
		}

		// Validate it's this player's turn to draw
		var (currentDrawPlayerId, _) = game.GetCurrentDrawPlayer();
		if (currentDrawPlayerId != request.PlayerId)
		{
			return TypedResults.BadRequest("It is not this player's turn to draw.");
		}

		// Process the draw
		var result = game.ProcessDraw(request.PlayerId, request.DiscardIndices);

		if (!result.Success)
		{
			return TypedResults.BadRequest(result.ErrorMessage ?? "Failed to process draw.");
		}

		// Create and append the event
		var drawEvent = new DrawCardsPerformed(
			gameId,
			game.CurrentHandId.Value,
			request.PlayerId,
			result.CardsDiscarded,
			result.NewHand,
			result.DrawPhaseComplete,
			result.NextPlayerToAct,
			DateTime.UtcNow
		);

		session.Events.Append(gameId, drawEvent);
		await session.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(new DrawCardsResponse(
			Success: true,
			CardsDiscarded: result.CardsDiscarded,
			NewCards: result.NewCards,
			NewHand: result.NewHand,
			DrawPhaseComplete: result.DrawPhaseComplete,
			NextPlayerToAct: result.NextPlayerToAct,
			CurrentPhase: game.CurrentPhase.ToString()
		));
	}
}
