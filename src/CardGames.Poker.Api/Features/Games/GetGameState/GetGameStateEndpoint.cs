namespace CardGames.Poker.Api.Features.Games.GetGameState;

using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.JoinGame;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

/// <summary>
/// Wolverine HTTP endpoint for retrieving game state.
/// </summary>
public static class GetGameStateEndpoint
{
	/// <summary>
	/// Gets the current state of a poker game.
	/// </summary>
	[WolverineGet("/api/v1/games/{gameId}")]
	public static async Task<Results<Ok<GetGameStateResponse>, NotFound<string>>> Get(
		Guid gameId,
		IDocumentSession session,
		CancellationToken cancellationToken)
	{
		// Load the current game state from events
		var game = await session.Events.AggregateStreamAsync<PokerGameAggregate>(
			gameId,
			token: cancellationToken);

		if (game == null)
		{
			return TypedResults.NotFound($"Game with ID {gameId} not found.");
		}

		// Map to response
		var players = game.Players
			.Select(p => new PlayerStateResponse(
				p.PlayerId,
				p.Name,
				p.ChipStack,
				p.Position,
				PlayerStatus.Active // In Phase 1, all players are active until game starts
			))
			.ToList();

		return TypedResults.Ok(new GetGameStateResponse(
			game.Id,
			game.GameType,
			game.Status,
			game.Configuration,
			players,
			DealerPosition: 0, // Dealer position is 0 until game starts
			game.CreatedAt
		));
	}
}