using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace CardGames.Poker.Api.Features.Games.GetCurrentHand;

public static class GetCurrentHandEndpoint
{
	[WolverineGet("/api/v1/games/{gameId}/hands/current")]
	[EndpointName("GetCurrentHand")]
	public static async Task<Results<Ok<GetCurrentHandResponse>, NotFound<string>>> Get(
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

		if (game.CurrentHandId == null || game.CurrentPhase == HandPhase.None)
		{
			return TypedResults.NotFound("No active hand in this game.");
		}

		var players = game.Players.Select(p => new HandPlayerStateResponse(
			p.PlayerId,
			p.Name,
			p.ChipStack,
			p.CurrentBet,
			GetPlayerStatus(p),
			p.Cards.Count
		)).ToList();

		return TypedResults.Ok(new GetCurrentHandResponse(
			game.CurrentHandId.Value,
			game.HandNumber,
			game.CurrentPhase.ToString(),
			game.TotalPot,
			game.CurrentPlayerToAct,
			game.CurrentBet,
			game.DealerPosition,
			players
		));
	}

	private static string GetPlayerStatus(GamePlayer player)
	{
		if (player.HasFolded)
			return "Folded";
		if (player.IsAllIn)
			return "AllIn";
		return "Active";
	}
}
