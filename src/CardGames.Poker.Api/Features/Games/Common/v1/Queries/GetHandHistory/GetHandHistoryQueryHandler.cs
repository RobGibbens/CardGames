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
				.ThenInclude(pr => pr.Player)
			.Where(h => h.GameId == request.GameId)
			.OrderByDescending(h => h.CompletedAtUtc)
			.Skip(request.Skip)
			.Take(request.Take)
			.AsSplitQuery()
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		// Get all player IDs from histories that reached showdown
		var playerIdsWithShowdown = histories
			.SelectMany(h => h.PlayerResults.Where(pr => pr.ReachedShowdown).Select(pr => new { pr.PlayerId, h.HandNumber }))
			.ToList();

		// Get cards for players who reached showdown
		var handNumbers = histories.Select(h => h.HandNumber).ToHashSet();
		var gameCards = await context.GameCards
			.Where(gc => gc.GameId == request.GameId && handNumbers.Contains(gc.HandNumber))
			.Where(gc => gc.GamePlayerId != null && gc.Location == Data.Entities.CardLocation.Hole)
			.Include(gc => gc.GamePlayer)
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		// Group cards by player and hand number
		var cardsByPlayerAndHand = gameCards
			.Where(gc => gc.GamePlayer != null)
			.GroupBy(gc => new { gc.GamePlayer!.PlayerId, gc.HandNumber })
			.ToDictionary(
				g => g.Key,
				g => g.OrderBy(gc => gc.DealOrder).Select(gc => FormatCard(gc.Symbol, gc.Suit)).ToList());

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
					// Get player's actual name from Player entity, fallback to stored name if not available
					var playerName = pr.Player?.Name ?? pr.PlayerName;

					// Get cards for this player if they reached showdown
					List<string>? visibleCards = null;
					if (pr.ReachedShowdown && 
						cardsByPlayerAndHand.TryGetValue(new { pr.PlayerId, h.HandNumber }, out var cards))
					{
						visibleCards = cards;
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

	/// <summary>
	/// Formats a card as text (e.g., "3s", "Ah", "10d").
	/// </summary>
	private static string FormatCard(Data.Entities.CardSymbol symbol, Data.Entities.CardSuit suit)
	{
		var symbolStr = symbol switch
		{
			Data.Entities.CardSymbol.Deuce => "2",
			Data.Entities.CardSymbol.Three => "3",
			Data.Entities.CardSymbol.Four => "4",
			Data.Entities.CardSymbol.Five => "5",
			Data.Entities.CardSymbol.Six => "6",
			Data.Entities.CardSymbol.Seven => "7",
			Data.Entities.CardSymbol.Eight => "8",
			Data.Entities.CardSymbol.Nine => "9",
			Data.Entities.CardSymbol.Ten => "10",
			Data.Entities.CardSymbol.Jack => "J",
			Data.Entities.CardSymbol.Queen => "Q",
			Data.Entities.CardSymbol.King => "K",
			Data.Entities.CardSymbol.Ace => "A",
			_ => "?"
		};

		var suitStr = suit switch
		{
			Data.Entities.CardSuit.Hearts => "h",
			Data.Entities.CardSuit.Diamonds => "d",
			Data.Entities.CardSuit.Spades => "s",
			Data.Entities.CardSuit.Clubs => "c",
			_ => "?"
		};

		return $"{symbolStr}{suitStr}";
	}
}
