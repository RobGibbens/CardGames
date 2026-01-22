using Contracts = CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetHandHistory;

/// <summary>
/// Handles the <see cref="GetHandHistoryQuery"/> to retrieve hand history for a game.
/// </summary>
public class GetHandHistoryQueryHandler(CardsDbContext context)
	: IRequestHandler<GetHandHistoryQuery, Contracts.HandHistoryListDto>
{
	/// <inheritdoc />
	public async Task<Contracts.HandHistoryListDto> Handle(GetHandHistoryQuery request, CancellationToken cancellationToken)
	{
		// Get total count
		var totalCount = await context.HandHistories
			.Where(h => h.GameId == request.GameId)
			.CountAsync(cancellationToken);

		// Get hand history entries, ordered newest-first
		var histories = await context.HandHistories
			.Include(h => h.Winners)
				.ThenInclude(w => w.Player)
			.Include(h => h.PlayerResults)
			.Where(h => h.GameId == request.GameId)
			.OrderByDescending(h => h.CompletedAtUtc)
			.Skip(request.Skip)
			.Take(request.Take)
			.AsSplitQuery()
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		// Get winner emails for display name resolution (only Winners need Player loaded)
		var winnerEmails = histories
			.SelectMany(h => h.Winners)
			.Select(w => w.Player?.Email)
			.Where(email => !string.IsNullOrWhiteSpace(email))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList()!;

		var firstNamesByEmail = winnerEmails.Count == 0
			? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
			: await context.Users
				.AsNoTracking()
				.Where(u => u.Email != null && winnerEmails.Contains(u.Email))
				.Select(u => new { Email = u.Email!, u.FirstName })
				.ToDictionaryAsync(u => u.Email, u => u.FirstName, StringComparer.OrdinalIgnoreCase, cancellationToken);

		var entries = histories.Select(h =>
		{
			// Get winner display name
			string GetWinnerDisplayName(HandHistoryWinner w)
			{
				var email = w.Player?.Email;
				if (!string.IsNullOrWhiteSpace(email) &&
					firstNamesByEmail.TryGetValue(email, out var firstName) &&
					!string.IsNullOrWhiteSpace(firstName))
				{
					return firstName;
				}
				return w.PlayerName;
			}

			var winnerDisplay = h.Winners.Count switch
			{
				0 => "Unknown",
				1 => GetWinnerDisplayName(h.Winners.First()),
				_ => $"{GetWinnerDisplayName(h.Winners.First())} +{h.Winners.Count - 1}"
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
					currentPlayerWon = currentPlayerResult.ResultType == PlayerResultType.Won ||
									   currentPlayerResult.ResultType == PlayerResultType.SplitPotWon;
				}
			}

			// Map all player results for expandable display
			// Use stored PlayerName directly - no need to load Player navigation property
			var playerResults = h.PlayerResults
				.OrderBy(pr => pr.SeatPosition)
				.Select(pr => MapToPlayerResultDto(pr, pr.PlayerName))
				.ToList();

			return new Contracts.HandHistoryEntryDto(
				amountWon: totalWinnings,
				completedAtUtc: h.CompletedAtUtc,
				currentPlayerNetDelta: currentPlayerNetDelta,
				currentPlayerResultLabel: currentPlayerResultLabel ?? string.Empty,
				currentPlayerWon: currentPlayerWon,
				handNumber: h.HandNumber,
				winnerCount: h.Winners.Count,
				winnerName: winnerDisplay,
				winningHandDescription: h.WinningHandDescription ?? string.Empty,
				wonByFold: h.EndReason == HandEndReason.FoldedToWinner
			)
			{
				PlayerResults = playerResults
			};
		}).ToList();

		return new Contracts.HandHistoryListDto
		(
			entries:entries,
			hasMore: request.Skip + request.Take < totalCount,
			totalHands: totalCount
		);
	}

	private static Contracts.HandHistoryPlayerResultDto MapToPlayerResultDto(
		HandHistoryPlayerResult pr,
		string displayName)
	{
		var finalAction = pr.ResultType switch
		{
			PlayerResultType.Won => Contracts.PlayerFinalAction.Won,
			PlayerResultType.SplitPotWon => Contracts.PlayerFinalAction.SplitPot,
			PlayerResultType.Folded => Contracts.PlayerFinalAction.Folded,
			PlayerResultType.Lost => Contracts.PlayerFinalAction.Lost,
			_ => Contracts.PlayerFinalAction.Lost
		};

		// Parse visible cards if player reached showdown
		var visibleCards = ParseVisibleCards(pr.FinalVisibleCards, pr.ReachedShowdown);

		return new Contracts.HandHistoryPlayerResultDto
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

	private static IReadOnlyList<Contracts.HandHistoryCardDto>? ParseVisibleCards(string? cardsString, bool reachedShowdown)
	{
		// Only show cards for players who reached showdown
		if (!reachedShowdown || string.IsNullOrWhiteSpace(cardsString))
		{
			return null;
		}

		var cardCodes = cardsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var cards = new List<Contracts.HandHistoryCardDto>();

		foreach (var code in cardCodes)
		{
			var (rank, suit) = ParseCardCode(code);
			if (rank != null && suit != null)
			{
				cards.Add(new Contracts.HandHistoryCardDto { Rank = rank, Suit = suit });
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
