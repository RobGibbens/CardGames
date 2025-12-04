using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace CardGames.Poker.Api.Features.Games.DealCards;

/// <summary>
/// Endpoint for dealing cards to all active players.
/// This also handles collecting antes if not yet collected.
/// </summary>
public static class DealCardsEndpoint
{
    [WolverinePost("/api/v1/games/{gameId}/hands/current/deal")]
    [EndpointName("DealCards")]
    public static async Task<Results<Ok<DealCardsResponse>, NotFound<string>, BadRequest<string>>> Post(
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
            return TypedResults.BadRequest("No active hand in this game. Start a hand first.");
        }

        // Handle CollectingAntes phase - collect antes first
        if (game.CurrentPhase == HandPhase.CollectingAntes)
        {
            var antesResult = game.CollectAntes();
            if (!antesResult.Success)
            {
                return TypedResults.BadRequest(antesResult.ErrorMessage ?? "Failed to collect antes.");
            }

            // Append the antes collected event
            var antesEvent = new AntesCollected(
                gameId,
                game.CurrentHandId.Value,
                antesResult.PlayerAntes,
                antesResult.TotalCollected,
                DateTime.UtcNow
            );
            session.Events.Append(gameId, antesEvent);
        }

        // Validate we're in the dealing phase
        if (game.CurrentPhase != HandPhase.Dealing && game.CurrentPhase != HandPhase.CollectingAntes)
        {
            return TypedResults.BadRequest($"Cannot deal cards in phase {game.CurrentPhase}. Hand must be in CollectingAntes or Dealing phase.");
        }

        // Deal cards
        var dealResult = game.DealCards();
        if (!dealResult.Success)
        {
            return TypedResults.BadRequest(dealResult.ErrorMessage ?? "Failed to deal cards.");
        }

        // Append the cards dealt events
        var cardsDealtEvent = new CardsDealt(
            gameId,
            game.CurrentHandId.Value,
            dealResult.PlayerCardCounts,
            DateTime.UtcNow
        );

        var cardsDealtInternalEvent = new CardsDealtInternal(
            gameId,
            game.CurrentHandId.Value,
            dealResult.PlayerCards,
            DateTime.UtcNow
        );

        session.Events.Append(gameId, cardsDealtEvent);
        session.Events.Append(gameId, cardsDealtInternalEvent);
        await session.SaveChangesAsync(cancellationToken);

        var playerCardCounts = game.Players.Select(p => new PlayerCardCountResponse(
            p.PlayerId,
            p.Name,
            p.Cards.Count
        )).ToList();

        return TypedResults.Ok(new DealCardsResponse(
            Success: true,
            Phase: game.CurrentPhase.ToString(),
            PlayerCardCounts: playerCardCounts,
            CurrentPlayerToAct: game.CurrentPlayerToAct
        ));
    }
}
