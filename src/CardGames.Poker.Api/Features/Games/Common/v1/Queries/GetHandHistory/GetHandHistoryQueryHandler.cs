using CardGames.Contracts.SignalR;
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

		// Get all player IDs from the histories
		var allPlayerIds = histories
			.SelectMany(h => h.PlayerResults)
			.Select(pr => pr.PlayerId)
			.Distinct()
			.ToList();

		// Load all players separately
		var playersByIdLookup = await context.Players
			.Where(p => allPlayerIds.Contains(p.Id))
			.AsNoTracking()
			.ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

		// Cards are now stored in HandHistoryPlayerResult.ShowdownCards (JSON)
		// No need to query GameCards table

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

			// Map all player results
			var playerResults = h.PlayerResults
				.OrderBy(pr => pr.SeatPosition)
				.Select(pr =>
				{
					// Get player's actual name from Players lookup, fallback to stored name if not available
					var playerName = playersByIdLookup.TryGetValue(pr.PlayerId, out var name) ? name : pr.PlayerName;

					// Get cards for this player if they reached showdown (stored in ShowdownCards JSON)
					List<string>? visibleCards = null;
					if (pr.ReachedShowdown && !string.IsNullOrWhiteSpace(pr.ShowdownCards))
					{
						try
						{
							visibleCards = System.Text.Json.JsonSerializer.Deserialize<List<string>>(pr.ShowdownCards);
						}
						catch (System.Text.Json.JsonException)
						{
							// Log error but don't fail the request
							visibleCards = null;
						}
					}

					return new PlayerHandResultDto
					{
						PlayerId = pr.PlayerId,
						PlayerName = playerName,
						SeatPosition = pr.SeatPosition,
						ResultType = pr.ResultType.ToString(),
						ResultLabel = pr.GetResultLabel(),
						NetAmount = pr.NetChipDelta,
						ReachedShowdown = pr.ReachedShowdown,
						VisibleCards = visibleCards
					};
				})
				.ToList();

			return new HandHistoryEntryDto
			{
				HandNumber = h.HandNumber,
				CompletedAtUtc = h.CompletedAtUtc,
				WinnerName = winnerDisplay,
				AmountWon = totalWinnings,
				WinningHandDescription = h.WinningHandDescription,
				WonByFold = h.EndReason == Data.Entities.HandEndReason.FoldedToWinner,
				WinnerCount = h.Winners.Count,
				PlayerResults = playerResults
			};
		}).ToList();

		return new HandHistoryListDto
		{
			Entries = entries,
			HasMore = request.Skip + request.Take < totalCount,
			TotalHands = totalCount
		};
	}
}
