using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetHandHistory;

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

		var winnerEmails = histories
			.SelectMany(h => h.Winners)
			.Select(w => w.Player.Email)
			.Where(email => !string.IsNullOrWhiteSpace(email))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var winnerFirstNamesByEmail = winnerEmails.Count == 0
			? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
			: await context.Users
				.AsNoTracking()
				.Where(u => u.Email != null && winnerEmails.Contains(u.Email))
				.Select(u => new { Email = u.Email!, u.FirstName })
				.ToDictionaryAsync(u => u.Email, u => u.FirstName, StringComparer.OrdinalIgnoreCase, cancellationToken);

		var entries = histories.Select(h =>
		{
			// Get winner display
			string GetWinnerFirstNameOrFallback()
			{
				var firstWinner = h.Winners.First();
				var email = firstWinner.Player.Email;

				if (!string.IsNullOrWhiteSpace(email) &&
					winnerFirstNamesByEmail.TryGetValue(email, out var firstName) &&
					!string.IsNullOrWhiteSpace(firstName))
				{
					return firstName;
				}

				return firstWinner.PlayerName;
			}

			var winnerDisplay = h.Winners.Count switch
			{
				0 => "Unknown",
				1 => GetWinnerFirstNameOrFallback(),
				_ => $"{GetWinnerFirstNameOrFallback()} +{h.Winners.Count - 1}"
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

			return new HandHistoryEntryDto(
				amountWon: totalWinnings,
				completedAtUtc: h.CompletedAtUtc,
				currentPlayerNetDelta: currentPlayerNetDelta,
				currentPlayerResultLabel: currentPlayerResultLabel,
				currentPlayerWon: currentPlayerWon,
				handNumber: h.HandNumber,
				winnerCount: h.Winners.Count,
				winnerName: winnerDisplay,
				winningHandDescription: h.WinningHandDescription,
				wonByFold: h.EndReason == Data.Entities.HandEndReason.FoldedToWinner
			);
		}).ToList();

		return new HandHistoryListDto
		(
			entries:entries,
			hasMore: request.Skip + request.Take < totalCount,
			totalHands: totalCount
		);
	}
}
