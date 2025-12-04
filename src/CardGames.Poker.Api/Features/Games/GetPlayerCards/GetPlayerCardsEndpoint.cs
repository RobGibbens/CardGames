using CardGames.Poker.Api.Features.Games.Domain;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace CardGames.Poker.Api.Features.Games.GetPlayerCards;

/// <summary>
/// Endpoint for retrieving a player's cards.
/// </summary>
public static class GetPlayerCardsEndpoint
{
    [WolverineGet("/api/v1/games/{gameId}/players/{playerId}/cards")]
    [EndpointName("GetPlayerCards")]
    public static async Task<Results<Ok<GetPlayerCardsResponse>, NotFound<string>>> Get(
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

        return TypedResults.Ok(new GetPlayerCardsResponse(
            playerId,
            player.Cards,
            player.Cards.Count
        ));
    }
}
