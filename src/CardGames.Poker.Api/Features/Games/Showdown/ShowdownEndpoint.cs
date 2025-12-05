using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace CardGames.Poker.Api.Features.Games.Showdown;

/// <summary>
/// Endpoint for performing the showdown and determining winners.
/// </summary>
public static class ShowdownEndpoint
{
	[WolverinePost("/api/v1/games/{gameId}/hands/current/showdown")]
	[EndpointName("Showdown")]
	public static async Task<Results<Ok<ShowdownResponse>, NotFound<string>, BadRequest<string>>> Post(
		Guid gameId,
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

		if (!game.CanPerformShowdown())
		{
			return TypedResults.BadRequest($"Cannot perform showdown in phase {game.CurrentPhase}.");
		}

		// Perform the showdown
		var result = game.PerformShowdown();

		if (!result.Success)
		{
			return TypedResults.BadRequest(result.ErrorMessage ?? "Failed to perform showdown.");
		}

		// Create and append the event
		var showdownEventResults = result.Results.Select(r =>
		{
			var player = game.Players.FirstOrDefault(p => p.PlayerId == r.PlayerId);
			return new ShowdownPlayerEventResult(
				r.PlayerId,
				r.PlayerName,
				r.Hand,
				r.HandType,
				r.HandDescription,
				r.Payout,
				r.IsWinner,
				player?.ChipStack ?? 0
			);
		}).ToList();

		var showdownEvent = new ShowdownPerformed(
			gameId,
			game.CurrentHandId.Value,
			result.WonByFold,
			showdownEventResults,
			DateTime.UtcNow
		);

		session.Events.Append(gameId, showdownEvent);
		await session.SaveChangesAsync(cancellationToken);

		var responseResults = result.Results.Select(r => new ShowdownPlayerResponse(
			r.PlayerId,
			r.PlayerName,
			r.Hand,
			r.HandType,
			r.HandDescription,
			r.Payout,
			r.IsWinner
		)).ToList();

		return TypedResults.Ok(new ShowdownResponse(
			Success: true,
			WonByFold: result.WonByFold,
			Results: responseResults
		));
	}
}
