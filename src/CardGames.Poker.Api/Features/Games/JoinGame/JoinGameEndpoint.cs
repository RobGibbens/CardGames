using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace CardGames.Poker.Api.Features.Games.JoinGame;

/// <summary>
/// Wolverine HTTP endpoint for joining an existing poker game.
/// </summary>
public static class JoinGameEndpoint
{
	/// <summary>
	/// Adds a player to an existing game.
	/// </summary>
	[WolverinePost("/api/v1/games/{gameId}/players")]
	public static async Task<Results<Ok<JoinGameResponse>, NotFound<string>, BadRequest<string>>> Post(
		Guid gameId,
		JoinGameRequest request,
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

		// Validate game state
		if (!game.CanPlayerJoin())
		{
			return TypedResults.BadRequest("Cannot join game - game is not accepting new players.");
		}

		if (game.IsFull())
		{
			return TypedResults.BadRequest($"Cannot join game - game is full ({game.Configuration.MaxPlayers} players max).");
		}

		// Check for duplicate player names
		if (game.Players.Any(p => p.Name.Equals(request.PlayerName, StringComparison.OrdinalIgnoreCase)))
		{
			return TypedResults.BadRequest($"A player named '{request.PlayerName}' is already in the game.");
		}

		var playerId = Guid.NewGuid();
		var buyIn = request.BuyIn ?? game.Configuration.StartingChips;
		var position = game.GetNextPosition();

		// Create and append the event
		var playerJoinedEvent = new PlayerJoined(
			gameId,
			playerId,
			request.PlayerName,
			buyIn,
			position,
			DateTime.UtcNow
		);

		session.Events.Append(gameId, playerJoinedEvent);
		await session.SaveChangesAsync(cancellationToken);

		return TypedResults.Ok(new JoinGameResponse(
			playerId,
			request.PlayerName,
			buyIn,
			position,
			PlayerStatus.Active
		));
	}
}