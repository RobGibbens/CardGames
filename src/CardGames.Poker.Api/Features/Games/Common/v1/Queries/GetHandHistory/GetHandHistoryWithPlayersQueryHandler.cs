using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetHandHistory;

/// <summary>
/// Handles the <see cref="GetHandHistoryWithPlayersQuery"/> to retrieve hand history with per-player results.
/// </summary>
public class GetHandHistoryWithPlayersQueryHandler(CardsDbContext context)
    : IRequestHandler<GetHandHistoryWithPlayersQuery, HandHistoryWithPlayersListDto>
{
    /// <inheritdoc />
    public async Task<HandHistoryWithPlayersListDto> Handle(
        GetHandHistoryWithPlayersQuery request,
        CancellationToken cancellationToken)
    {
        // Get total count
        var totalCount = await context.HandHistories
            .Where(h => h.GameId == request.GameId)
            .CountAsync(cancellationToken);

        // Get hand history entries with all player results, ordered newest-first
        var histories = await context.HandHistories
            .Include(h => h.Winners)
                .ThenInclude(w => w.Player)
            .Include(h => h.PlayerResults)
                .ThenInclude(pr => pr.Player)
            .Where(h => h.GameId == request.GameId)
            .OrderByDescending(h => h.CompletedAtUtc)
            .Skip(request.Skip)
            .Take(request.Take)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Get player first names by email for display
        var playerEmails = histories
            .SelectMany(h => h.PlayerResults)
            .Select(pr => pr.Player.Email)
            .Union(histories.SelectMany(h => h.Winners).Select(w => w.Player.Email))
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var firstNamesByEmail = playerEmails.Count == 0
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : await context.Users
                .AsNoTracking()
                .Where(u => u.Email != null && playerEmails.Contains(u.Email))
                .Select(u => new { Email = u.Email!, u.FirstName })
                .ToDictionaryAsync(u => u.Email, u => u.FirstName, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var entries = histories.Select(h => MapToEntryDto(h, firstNamesByEmail)).ToList();

        return new HandHistoryWithPlayersListDto
        {
            Entries = entries,
            HasMore = request.Skip + request.Take < totalCount,
            TotalHands = totalCount
        };
    }

    private static HandHistoryEntryWithPlayersDto MapToEntryDto(
        HandHistory h,
        Dictionary<string, string?> firstNamesByEmail)
    {
        string GetFirstNameOrFallback(string? email, string fallbackName)
        {
            if (!string.IsNullOrWhiteSpace(email) &&
                firstNamesByEmail.TryGetValue(email, out var firstName) &&
                !string.IsNullOrWhiteSpace(firstName))
            {
                return firstName;
            }
            return fallbackName;
        }

        // Get winner display name
        var winnerDisplay = h.Winners.Count switch
        {
            0 => "Unknown",
            1 => GetFirstNameOrFallback(h.Winners.First().Player.Email, h.Winners.First().PlayerName),
            _ => $"{GetFirstNameOrFallback(h.Winners.First().Player.Email, h.Winners.First().PlayerName)} +{h.Winners.Count - 1}"
        };

        var totalPot = h.Winners.Sum(w => w.AmountWon);

        // Map player results
        var playerResults = h.PlayerResults
            .OrderBy(pr => pr.SeatPosition)
            .Select(pr => MapToPlayerResultDto(pr, GetFirstNameOrFallback(pr.Player.Email, pr.PlayerName)))
            .ToList();

        return new HandHistoryEntryWithPlayersDto
        {
            HandId = h.Id,
            HandNumber = h.HandNumber,
            WinnerName = winnerDisplay,
            TotalPotAmount = totalPot,
            WinnerCount = h.Winners.Count,
            WinningHandDescription = h.WinningHandDescription,
            WonByFold = h.EndReason == HandEndReason.FoldedToWinner,
            CompletedAtUtc = h.CompletedAtUtc,
            PlayerResults = playerResults
        };
    }

    private static HandHistoryPlayerResultDto MapToPlayerResultDto(
        HandHistoryPlayerResult pr,
        string displayName)
    {
        var finalAction = pr.ResultType switch
        {
            PlayerResultType.Won => PlayerFinalAction.Won,
            PlayerResultType.SplitPotWon => PlayerFinalAction.SplitPot,
            PlayerResultType.Folded => PlayerFinalAction.Folded,
            PlayerResultType.Lost => PlayerFinalAction.Lost,
            _ => PlayerFinalAction.Lost
        };

        // Parse visible cards if player reached showdown
        var visibleCards = ParseVisibleCards(pr.FinalVisibleCards, pr.ReachedShowdown);

        return new HandHistoryPlayerResultDto
        {
            PlayerId = pr.PlayerId,
            PlayerName = displayName,
            FinalAction = finalAction,
            NetAmount = pr.NetChipDelta,
            FinalVisibleCards = visibleCards,
            SeatPosition = pr.SeatPosition,
            ReachedShowdown = pr.ReachedShowdown,
            ResultLabel = pr.GetResultLabel()
        };
    }

    private static IReadOnlyList<HandHistoryCardDto>? ParseVisibleCards(string? cardsString, bool reachedShowdown)
    {
        // Only show cards for players who reached showdown
        if (!reachedShowdown || string.IsNullOrWhiteSpace(cardsString))
        {
            return null;
        }

        var cardCodes = cardsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cards = new List<HandHistoryCardDto>();

        foreach (var code in cardCodes)
        {
            var (rank, suit) = ParseCardCode(code);
            if (rank != null && suit != null)
            {
                cards.Add(new HandHistoryCardDto { Rank = rank, Suit = suit });
            }
        }

        return cards.Count > 0 ? cards : null;
    }

    private static (string? Rank, string? Suit) ParseCardCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 2)
        {
            return (null, null);
        }

        // Card codes are like "As", "Kh", "10d", "2c"
        // Rank is all characters except the last, suit is the last character
        var suitChar = code[^1];
        var rankPart = code[..^1];

        var rank = rankPart.ToUpperInvariant() switch
        {
            "A" => "A",
            "K" => "K",
            "Q" => "Q",
            "J" => "J",
            "10" => "10",
            "9" => "9",
            "8" => "8",
            "7" => "7",
            "6" => "6",
            "5" => "5",
            "4" => "4",
            "3" => "3",
            "2" => "2",
            _ => rankPart
        };

        var suit = char.ToLowerInvariant(suitChar) switch
        {
            's' => "Spades",
            'h' => "Hearts",
            'd' => "Diamonds",
            'c' => "Clubs",
            _ => null
        };

        return (rank, suit);
    }
}
