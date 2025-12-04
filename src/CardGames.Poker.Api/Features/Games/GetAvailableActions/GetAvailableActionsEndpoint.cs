using CardGames.Poker.Api.Features.Games.Domain;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace CardGames.Poker.Api.Features.Games.GetAvailableActions;

public static class GetAvailableActionsEndpoint
{
	[WolverineGet("/api/v1/games/{gameId}/players/{playerId}/available-actions")]
	[EndpointName("GetAvailableActions")]
	public static async Task<Results<Ok<GetAvailableActionsResponse>, NotFound<string>>> Get(
		Guid gameId,
		Guid playerId,
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

		var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
		if (player == null)
		{
			return TypedResults.NotFound($"Player with ID {playerId} not found in game.");
		}

		var isCurrentPlayer = game.IsPlayerTurn(playerId);
		var actions = isCurrentPlayer
			? game.GetAvailableActions()
			: new Betting.AvailableActions(); // Empty actions if not current player

		return TypedResults.Ok(new GetAvailableActionsResponse(
			playerId,
			isCurrentPlayer,
			new AvailableActionsDto(
				actions.CanCheck,
				actions.CanBet,
				actions.CanCall,
				actions.CanRaise,
				actions.CanFold,
				actions.CanAllIn,
				actions.MinBet,
				actions.MaxBet,
				actions.CallAmount,
				actions.MinRaise
			)
		));
	}
}
