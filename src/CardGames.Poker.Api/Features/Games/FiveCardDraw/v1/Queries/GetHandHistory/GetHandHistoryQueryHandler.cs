using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetHandHistory;

/// <summary>
/// Handles the <see cref="GetHandHistoryQuery"/> to retrieve hand history for a game.
/// </summary>
public class GetHandHistoryQueryHandler(CardsDbContext context)
    : IRequestHandler<GetHandHistoryQuery, HandHistoryListDto>
{
    /// <inheritdoc />
    public async Task<HandHistoryListDto> Handle(GetHandHistoryQuery request, CancellationToken cancellationToken)
    {
        // Get total count
        var totalCount = await context.HandHistories
            .Where(h => h.GameId == request.GameId)
            .CountAsync(cancellationToken);

        // Get hand history entries, ordered newest-first
        var histories = await context.HandHistories
            .Where(h => h.GameId == request.GameId)
            .OrderByDescending(h => h.CompletedAtUtc)
            .Skip(request.Skip)
            .Take(request.Take)
            .Include(h => h.Winners)
            .Include(h => h.PlayerResults)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var entries = histories.Select(h =>
        {
            // Get winner display
            var winnerDisplay = h.Winners.Count switch
            {
                0 => "Unknown",
                1 => h.Winners.First().PlayerName,
                _ => $"{h.Winners.First().PlayerName} +{h.Winners.Count - 1}"
            };

            var totalWinnings = h.Winners.Sum(w => w.AmountWon);

            // Get current player's result if specified
            string? currentPlayerResultLabel = null;
            var currentPlayerNetDelta = 0;
            var currentPlayerWon = false;

            if (request.CurrentUserPlayerId.HasValue)
            {
                var currentPlayerResult = h.PlayerResults
                    .FirstOrDefault(pr => pr.PlayerId == request.CurrentUserPlayerId.Value);

                if (currentPlayerResult != null)
                {
                    currentPlayerResultLabel = currentPlayerResult.GetResultLabel();
                    currentPlayerNetDelta = currentPlayerResult.NetChipDelta;
                    currentPlayerWon = currentPlayerResult.ResultType == Data.Entities.PlayerResultType.Won ||
                                       currentPlayerResult.ResultType == Data.Entities.PlayerResultType.SplitPotWon;
                }
            }

            return new HandHistoryEntryDto
            {
                HandNumber = h.HandNumber,
                CompletedAtUtc = h.CompletedAtUtc,
                WinnerName = winnerDisplay,
                AmountWon = totalWinnings,
                WinningHandDescription = h.WinningHandDescription,
                WonByFold = h.EndReason == Data.Entities.HandEndReason.FoldedToWinner,
                WinnerCount = h.Winners.Count,
                CurrentPlayerResultLabel = currentPlayerResultLabel,
                CurrentPlayerNetDelta = currentPlayerNetDelta,
                CurrentPlayerWon = currentPlayerWon
            };
        }).ToList();

        return new HandHistoryListDto
        {
            Entries = entries,
            TotalHands = totalCount,
            HasMore = request.Skip + request.Take < totalCount
        };
    }
}
