using System.Text.Json;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Games.KingsAndLows;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.WildCards;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.PerformShowdown;

/// <summary>
/// Handles the <see cref="PerformShowdownCommand"/> to evaluate hands and award pots in Kings and Lows.
/// Uses wild card evaluation: Kings are always wild, plus the lowest non-King card(s).
/// </summary>
public class PerformShowdownCommandHandler(CardsDbContext context, IHandHistoryRecorder handHistoryRecorder)
	: IRequestHandler<PerformShowdownCommand, OneOf<PerformShowdownSuccessful, PerformShowdownError>>
{
	/// <inheritdoc />
	public async Task<OneOf<PerformShowdownSuccessful, PerformShowdownError>> Handle(
		PerformShowdownCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players, cards, and pots
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.Pots.Where(p => !p.IsAwarded))
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new PerformShowdownError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = PerformShowdownErrorCode.GameNotFound
			};
		}

		// Filter pots to current hand (in case there are old unawarded pots from edge cases)
		var currentHandPots = game.Pots.Where(p => p.HandNumber == game.CurrentHandNumber).ToList();

		// 2. Validate game is in showdown or complete phase
		if (game.CurrentPhase != nameof(KingsAndLowsPhase.Showdown) &&
		    game.CurrentPhase != nameof(KingsAndLowsPhase.Complete))
		{
			return new PerformShowdownError
			{
				Message = $"Cannot perform showdown. Game is in '{game.CurrentPhase}' phase. " +
				          $"Showdown can only be performed when the game is in '{nameof(KingsAndLowsPhase.Showdown)}' or '{nameof(KingsAndLowsPhase.Complete)}' phase.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};
		}

		// 3. Get players who have not folded (stayed in the hand)
		var playersInHand = game.GamePlayers
			.Where(gp => !gp.HasFolded && gp.Status == GamePlayerStatus.Active)
			.ToList();

		// Fetch user first names from Users table
		var playerEmails = game.GamePlayers
			.Where(gp => gp.Player.Email != null)
			.Select(gp => gp.Player.Email!)
			.ToList();

		var usersByEmail = await context.Users
			.AsNoTracking()
			.Where(u => u.Email != null && playerEmails.Contains(u.Email))
			.Select(u => new { Email = u.Email!, u.FirstName })
			.ToDictionaryAsync(u => u.Email, StringComparer.OrdinalIgnoreCase, cancellationToken);

		// 4. Load cards for players in hand
		var playerCards = await context.GameCards
			.Where(c => c.GameId == command.GameId &&
			            c.HandNumber == game.CurrentHandNumber &&
			            !c.IsDiscarded &&
			            c.GamePlayerId != null &&
			            playersInHand.Select(p => p.Id).Contains(c.GamePlayerId.Value))
			.ToListAsync(cancellationToken);

		var playerCardGroups = playerCards
			.GroupBy(c => c.GamePlayerId!.Value)
			.ToDictionary(g => g.Key, g => g.ToList());

		// 5. Calculate total pot (only from current hand's pots)
		var totalPot = currentHandPots.Sum(p => p.Amount);

		// 6. Handle win by fold (only one player remaining)
		if (playersInHand.Count == 1)
		{
			var winner = playersInHand[0];
			winner.ChipStack += totalPot;

			// Mark pots as awarded
			foreach (var pot in currentHandPots)
			{
				pot.IsAwarded = true;
				pot.AwardedAt = now;
				pot.WinReason = "All others folded";
			}

			game.CurrentPhase = nameof(KingsAndLowsPhase.Complete);
			game.UpdatedAt = now;
			game.HandCompletedAt = now;
			game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
			MoveDealer(game);

			await context.SaveChangesAsync(cancellationToken);

			// Record hand history for win-by-fold
			await RecordHandHistoryAsync(
				game,
				game.GamePlayers.ToList(),
				now,
				totalPot,
				wonByFold: true,
				winners: [(winner.PlayerId, winner.Player.Name, totalPot)],
				winnerNames: [winner.Player.Name],
				winningHandDescription: null,
				cancellationToken);

			var winnerCards = playerCardGroups.GetValueOrDefault(winner.Id, []);
			usersByEmail.TryGetValue(winner.Player.Email ?? string.Empty, out var winnerUser);

			return new PerformShowdownSuccessful
			{
				GameId = game.Id,
				WonByFold = true,
				CurrentPhase = game.CurrentPhase,
				Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
				Winners = [winner.Player.Name],
				Losers = [],
				PlayerHands =
				[
					new ShowdownPlayerHand
					{
						PlayerName = winner.Player.Name,
						PlayerFirstName = winnerUser?.FirstName,
						Cards = winnerCards.Select(c => new ShowdownCard
						{
							Suit = c.Suit,
							Symbol = c.Symbol
						}).ToList(),
						HandType = null,
						HandStrength = null,
						IsWinner = true,
						AmountWon = totalPot
					}
				]
			};
		}

		// 7. Evaluate all hands using Kings and Lows wild card rules
		var wildCardRules = new WildCardRules(kingRequired: false);
		var playerHandEvaluations = new Dictionary<string, (KingsAndLowsDrawHand hand, List<GameCard> cards, GamePlayer gamePlayer, List<int> wildIndexes)>();

		foreach (var gamePlayer in playersInHand)
		{
			if (!playerCardGroups.TryGetValue(gamePlayer.Id, out var cards) || cards.Count < 5)
			{
				continue; // Skip players without valid hands
			}

			var coreCards = cards.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol))).ToList();
			var wildHand = new KingsAndLowsDrawHand(coreCards);

			// Determine wild card indexes for UI display
			var wildCards = wildHand.WildCards;
			var wildIndexes = new List<int>();
			for (int i = 0; i < coreCards.Count; i++)
			{
				if (wildCards.Contains(coreCards[i]))
				{
					wildIndexes.Add(i);
				}
			}

			playerHandEvaluations[gamePlayer.Player.Name] = (wildHand, cards, gamePlayer, wildIndexes);
		}

		// 8. Determine winners (highest strength)
		var maxStrength = playerHandEvaluations.Values.Max(h => h.hand.Strength);
		var winners = playerHandEvaluations
			.Where(kvp => kvp.Value.hand.Strength == maxStrength)
			.Select(kvp => kvp.Key)
			.ToList();

		// Determine losers (players who didn't win)
		var losers = playerHandEvaluations.Keys.Except(winners).ToList();

		// 9. Calculate payouts (split pot if multiple winners)
		var payoutPerWinner = totalPot / winners.Count;
		var remainder = totalPot % winners.Count;
		var payouts = new Dictionary<string, int>();

		foreach (var winner in winners)
		{
			payouts[winner] = payoutPerWinner;
		}

		// Add remainder to first winner (closest to dealer's left)
		if (remainder > 0 && winners.Count > 0)
		{
			payouts[winners[0]] += remainder;
		}

		// 10. Update player chip stacks
		foreach (var payout in payouts)
		{
			var gamePlayer = playerHandEvaluations[payout.Key].gamePlayer;
			gamePlayer.ChipStack += payout.Value;
		}

		// 11. Mark pots as awarded and store winner information
		var winReason = winners.Count > 1
			? $"Split pot - {playerHandEvaluations[winners[0]].hand.Type}"
			: playerHandEvaluations[winners[0]].hand.Type.ToString();

		// Serialize winner payouts for pot matching phase
		var winnerPayoutsList = payouts.Select(p =>
		{
			var gp = playerHandEvaluations[p.Key].gamePlayer;
			return new { playerId = gp.PlayerId.ToString(), playerName = p.Key, amount = p.Value };
		}).ToList();
		var winnerPayoutsJson = JsonSerializer.Serialize(winnerPayoutsList);

		foreach (var pot in currentHandPots)
		{
			pot.IsAwarded = true;
			pot.AwardedAt = now;
			pot.WinReason = winReason;
			pot.WinnerPayouts = winnerPayoutsJson;
		}

		// 12. Update game state - transition to PotMatching if there are losers
		game.CurrentPhase = losers.Count > 0
			? nameof(KingsAndLowsPhase.PotMatching)
			: nameof(KingsAndLowsPhase.Complete);
		game.UpdatedAt = now;
		game.HandCompletedAt = now;
		game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
		MoveDealer(game);

		await context.SaveChangesAsync(cancellationToken);

		// Record hand history for showdown
		var winnerInfos = winners.Select(w =>
		{
			var gp = playerHandEvaluations[w].gamePlayer;
			return (gp.PlayerId, w, payouts[w]);
		}).ToList();

		await RecordHandHistoryAsync(
			game,
			game.GamePlayers.ToList(),
			now,
			totalPot,
			wonByFold: false,
			winners: winnerInfos,
			winnerNames: winners,
			winningHandDescription: winReason,
			cancellationToken);

		// 13. Build response with wild card info
		var playerHandsList = playerHandEvaluations.Select(kvp =>
		{
			var isWinner = winners.Contains(kvp.Key);
			usersByEmail.TryGetValue(kvp.Value.gamePlayer.Player.Email ?? string.Empty, out var user);
			var handDescription = HandDescriptionFormatter.GetHandDescription(kvp.Value.hand);
			return new ShowdownPlayerHand
			{
				PlayerName = kvp.Key,
				PlayerFirstName = user?.FirstName,
				Cards = kvp.Value.cards.Select(c => new ShowdownCard
				{
					Suit = c.Suit,
					Symbol = c.Symbol
				}).ToList(),
				HandType = kvp.Value.hand.Type.ToString(),
				HandDescription = handDescription,
				HandRanking = handDescription,
				HandStrength = kvp.Value.hand.Strength,
				IsWinner = isWinner,
				AmountWon = payouts.GetValueOrDefault(kvp.Key, 0),
				WildCardIndexes = kvp.Value.wildIndexes
			};
		}).OrderByDescending(h => h.HandStrength ?? 0).ToList();

		return new PerformShowdownSuccessful
		{
			GameId = game.Id,
			WonByFold = false,
			CurrentPhase = game.CurrentPhase,
			Payouts = payouts,
			Winners = winners,
			Losers = losers,
			PlayerHands = playerHandsList
		};
	}

	/// <summary>
	/// Moves the dealer button to the next occupied seat position (clockwise).
	/// </summary>
	private static void MoveDealer(Game game)
	{
		var occupiedSeats = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.Select(gp => gp.SeatPosition)
			.ToList();

		if (occupiedSeats.Count == 0)
		{
			return;
		}

		var currentPosition = game.DealerPosition;

		// Find next occupied seat clockwise from current position
		var seatsAfterCurrent = occupiedSeats.Where(pos => pos > currentPosition).ToList();

		if (seatsAfterCurrent.Count > 0)
		{
			game.DealerPosition = seatsAfterCurrent.First();
		}
		else
		{
			game.DealerPosition = occupiedSeats.First();
		}
	}

	/// <summary>
	/// Maps entity CardSuit to core library Suit.
	/// </summary>
	private static Suit MapSuit(CardSuit suit) => suit switch
	{
		CardSuit.Hearts => Suit.Hearts,
		CardSuit.Diamonds => Suit.Diamonds,
		CardSuit.Spades => Suit.Spades,
		CardSuit.Clubs => Suit.Clubs,
		_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit")
	};

		/// <summary>
		/// Maps entity CardSymbol to core library Symbol.
		/// </summary>
		private static Symbol MapSymbol(CardSymbol symbol) => symbol switch
		{
			CardSymbol.Deuce => Symbol.Deuce,
			CardSymbol.Three => Symbol.Three,
			CardSymbol.Four => Symbol.Four,
			CardSymbol.Five => Symbol.Five,
			CardSymbol.Six => Symbol.Six,
			CardSymbol.Seven => Symbol.Seven,
			CardSymbol.Eight => Symbol.Eight,
			CardSymbol.Nine => Symbol.Nine,
			CardSymbol.Ten => Symbol.Ten,
			CardSymbol.Jack => Symbol.Jack,
			CardSymbol.Queen => Symbol.Queen,
			CardSymbol.King => Symbol.King,
			CardSymbol.Ace => Symbol.Ace,
			_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown symbol")
		};

		/// <summary>
		/// Records hand history for the completed hand.
		/// </summary>
		private async Task RecordHandHistoryAsync(
			Game game,
			List<GamePlayer> allPlayers,
			DateTimeOffset completedAt,
			int totalPot,
			bool wonByFold,
			List<(Guid PlayerId, string PlayerName, int AmountWon)> winners,
			List<string> winnerNames,
			string? winningHandDescription,
			CancellationToken cancellationToken)
		{
			var isSplitPot = winners.Count > 1;
			var winnerNameSet = winnerNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

			// Build player results
			var playerResults = allPlayers.Select(gp =>
			{
				var isWinner = winnerNameSet.Contains(gp.Player.Name);
				var netDelta = isWinner
					? winners.First(w => w.PlayerId == gp.PlayerId).AmountWon - gp.TotalContributedThisHand
					: -gp.TotalContributedThisHand;

				return new PlayerResultInfo
				{
					PlayerId = gp.PlayerId,
					PlayerName = gp.Player.Name,
					SeatPosition = gp.SeatPosition,
					HasFolded = gp.HasFolded,
					ReachedShowdown = !gp.HasFolded && !wonByFold,
					IsWinner = isWinner,
					IsSplitPot = isSplitPot && isWinner,
					NetChipDelta = netDelta,
					WentAllIn = gp.IsAllIn,
					FoldStreet = gp.HasFolded ? "DropOrStay" : null
				};
			}).ToList();

			var winnerInfos = winners.Select(w => new WinnerInfo
			{
				PlayerId = w.PlayerId,
				PlayerName = w.PlayerName,
				AmountWon = w.AmountWon
			}).ToList();

			var parameters = new RecordHandHistoryParameters
			{
				GameId = game.Id,
				HandNumber = game.CurrentHandNumber,
				CompletedAtUtc = completedAt,
				WonByFold = wonByFold,
				TotalPot = totalPot,
				WinningHandDescription = winningHandDescription,
				Winners = winnerInfos,
				PlayerResults = playerResults
			};

			await handHistoryRecorder.RecordHandHistoryAsync(parameters, cancellationToken);
		}
	}
