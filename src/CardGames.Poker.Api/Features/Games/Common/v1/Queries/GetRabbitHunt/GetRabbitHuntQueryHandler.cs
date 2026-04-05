using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetRabbitHunt;

public sealed class GetRabbitHuntQueryHandler(
    CardsDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetRabbitHuntQuery, OneOf<GetRabbitHuntSuccessful, GetRabbitHuntError>>
{
    public async Task<OneOf<GetRabbitHuntSuccessful, GetRabbitHuntError>> Handle(GetRabbitHuntQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return new GetRabbitHuntError(GetRabbitHuntErrorCode.NotAuthenticated, "User not authenticated.");
        }

        var game = await context.Games
            .Include(g => g.GameType)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

        if (game is null)
        {
            return new GetRabbitHuntError(GetRabbitHuntErrorCode.GameNotFound, $"Game with ID '{request.GameId}' was not found.");
        }

        var gameTypeCode = game.GameType?.Code;
        if (!RabbitHuntBoardProjector.SupportsRabbitHunt(gameTypeCode))
        {
            return new GetRabbitHuntError(GetRabbitHuntErrorCode.UnsupportedGameType, "Rabbit Hunt is only available for supported community-card variants.");
        }

        var handIsOver = string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(game.CurrentPhase, "Complete", StringComparison.OrdinalIgnoreCase)
            || game.Status == CardGames.Poker.Api.Data.Entities.GameStatus.Completed;

        if (!handIsOver)
        {
            return new GetRabbitHuntError(GetRabbitHuntErrorCode.RabbitHuntNotAvailable, "Rabbit Hunt is only available after the hand is over.");
        }

        var userEmail = currentUserService.UserEmail;
        var userName = currentUserService.UserName;
        var userId = currentUserService.UserId;

        var isSeated = await context.GamePlayers
            .AsNoTracking()
            .Where(gp => gp.GameId == request.GameId && gp.Status != CardGames.Poker.Api.Data.Entities.GamePlayerStatus.Left)
            .Include(gp => gp.Player)
            .AnyAsync(gp =>
                (!string.IsNullOrWhiteSpace(userEmail) && gp.Player.Email == userEmail) ||
                (!string.IsNullOrWhiteSpace(userName) && gp.Player.Name == userName) ||
                (!string.IsNullOrWhiteSpace(userId) && gp.Player.ExternalId == userId),
                cancellationToken);

        if (!isSeated)
        {
            return new GetRabbitHuntError(GetRabbitHuntErrorCode.NotSeated, "Only seated players can request Rabbit Hunt for this hand.");
        }

        var currentCommunityCards = await context.GameCards
            .Where(card => card.GameId == request.GameId
                && card.HandNumber == game.CurrentHandNumber
                && !card.IsDiscarded
                && card.Location == CardLocation.Community
                && card.GamePlayerId == null)
            .OrderBy(card => card.DealOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var remainingDeckCards = await context.GameCards
            .Where(card => card.GameId == request.GameId
                && card.HandNumber == game.CurrentHandNumber
                && card.Location == CardLocation.Deck)
            .OrderBy(card => card.DealOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var projectedBoard = RabbitHuntBoardProjector.ProjectBoard(gameTypeCode!, currentCommunityCards, remainingDeckCards);
        var newlyRevealedCards = projectedBoard.Where(card => !card.WasAlreadyVisible).ToList();

        if (newlyRevealedCards.Count == 0)
        {
            return new GetRabbitHuntError(GetRabbitHuntErrorCode.RabbitHuntNotAvailable, "All community cards were already dealt and shown for this hand.");
        }

        return new GetRabbitHuntSuccessful
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            GameTypeCode = gameTypeCode!,
            CommunityCards = projectedBoard,
            PreviouslyVisibleCards = projectedBoard.Where(card => card.WasAlreadyVisible).ToList(),
            NewlyRevealedCards = newlyRevealedCards
        };
    }
}