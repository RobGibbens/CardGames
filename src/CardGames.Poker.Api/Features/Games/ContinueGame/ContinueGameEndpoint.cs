using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace CardGames.Poker.Api.Features.Games.ContinueGame;

/// <summary>
/// Endpoint for checking if game can continue and getting game end status.
/// </summary>
public static class ContinueGameEndpoint
{
	[WolverineGet("/api/v1/games/{gameId}/continue")]
	[EndpointName("ContinueGame")]
	public static async Task<Results<Ok<ContinueGameResponse>, NotFound<string>, BadRequest<string>>> Get(
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

		var playersWithChips = game.GetPlayersWithChips();
		var canContinue = game.CanContinueGame();

		var playerStatuses = game.Players.Select(p => new PlayerChipStatusResponse(
			p.PlayerId,
			p.Name,
			p.ChipStack,
			p.ChipStack > 0
		)).ToList();

		string? winnerName = null;
		int? winnerChips = null;

		if (!canContinue)
		{
			// Game is over - find the winner
			var winner = playersWithChips.FirstOrDefault();
			if (winner != null)
			{
				winnerName = winner.Name;
				winnerChips = winner.ChipStack;
			}
		}

		var status = canContinue
			? (game.CurrentPhase == HandPhase.None || game.CurrentPhase == HandPhase.Complete
				? "ReadyForNextHand"
				: "HandInProgress")
			: "GameOver";

		return TypedResults.Ok(new ContinueGameResponse(
			CanContinue: canContinue,
			Status: status,
			PlayersWithChips: playersWithChips.Count,
			Players: playerStatuses,
			WinnerName: winnerName,
			WinnerChips: winnerChips
		));
	}
}
