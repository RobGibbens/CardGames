using System.Text.Json;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.Showdown;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.GoodBadUgly.v1.Commands.PerformShowdown;

/// <summary>
///     Handles the <see cref="PerformShowdownCommand" /> to evaluate hands and award pots.
/// </summary>
public class PerformShowdownCommandHandler(
	CardsDbContext context,
	IHandHistoryRecorder handHistoryRecorder,
	IHandSettlementService handSettlementService)
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
			.Include(g => g.GameType)
			.Include(g => g.Pots)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
			return new PerformShowdownError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = PerformShowdownErrorCode.GameNotFound
			};

		// Filter pots to current hand (in case there are old unawarded pots from edge cases)
		var currentHandPots = game.Pots.Where(p => p.HandNumber == game.CurrentHandNumber).ToList();
		var isAlreadyAwarded = currentHandPots.Any(p => p.IsAwarded);

		// 2. Validate game is in showdown phase
		if (game.CurrentPhase != nameof(Phases.Showdown) && !isAlreadyAwarded)
			return new PerformShowdownError
			{
				Message = $"Cannot perform showdown. Game is in '{game.CurrentPhase}' phase. " +
				          $"Showdown can only be performed when the game is in '{nameof(Phases.Showdown)}' phase.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};

		// 3. Get players who have not folded
		var playersInHand = game.GamePlayers
			.Where(gp => !gp.HasFolded && (gp.Status == GamePlayerStatus.Active || gp.IsAllIn))
			.ToList();

		var isGoodBadUgly = string.Equals(game.GameType?.Code, "GOODBADUGLY", StringComparison.OrdinalIgnoreCase);

		// Fetch user first names from Users table (matching by email like GetGamePlayersQueryHandler)
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

			if (!isAlreadyAwarded)
			{
				var mismatch = ContributionLayerAwarder.ContributionMismatch(
					totalPot,
					game.GamePlayers.Sum(gamePlayer => gamePlayer.TotalContributedThisHand));
				if (mismatch is not null)
					return new PerformShowdownError
					{
						Message = mismatch,
						Code = PerformShowdownErrorCode.InvalidGameState
					};

				winner.ChipStack += totalPot;

				// Mark pots as awarded
				foreach (var pot in currentHandPots)
				{
					pot.IsAwarded = true;
					pot.AwardedAt = now;
					pot.WinReason = "All others folded";

					var winnerPayoutsList = new[]
					{
						new
						{
							playerId = winner.PlayerId.ToString(), playerName = winner.Player.Name, amount = totalPot
						}
					};
					pot.WinnerPayouts = JsonSerializer.Serialize(winnerPayoutsList);
				}

				game.CurrentPhase = nameof(Phases.Complete);
				game.UpdatedAt = now;
				game.HandCompletedAt = now;
				game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
				MoveDealer(game);

				// Settle win-by-fold to cashier ledger
				var foldPayouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } };
				await handSettlementService.SettleHandAsync(game, foldPayouts, cancellationToken);

				await context.SaveChangesAsync(cancellationToken);

				// Record hand history for win-by-fold
				await RecordHandHistoryAsync(
					game,
					game.GamePlayers.ToList(),
					now,
					totalPot,
					true,
					[(winner.PlayerId, winner.Player.Name, totalPot)],
					[winner.Player.Name],
					null,
					null,
					cancellationToken);
			}

			usersByEmail.TryGetValue(winner.Player.Email ?? string.Empty, out var winnerUser);

			return new PerformShowdownSuccessful
			{
				GameId = game.Id,
				WonByFold = true,
				CurrentPhase = game.CurrentPhase,
				Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
				PlayerHands =
				[
					new ShowdownPlayerHand
					{
						PlayerName = winner.Player.Name,
						PlayerFirstName = winnerUser?.FirstName,
						Cards = WinByFoldResponseBuilder.HiddenCards<ShowdownCard>(),
						HandType = null,
						HandStrength = null,
						IsWinner = true,
						AmountWon = totalPot
					}
				]
			};
		}

		// 7. Evaluate all hands
		var playerHandEvaluations =
			new Dictionary<string, (StudHand hand, List<GameCard> cards, GamePlayer gamePlayer)>();
		var goodBadUglyWildRules = new GoodBadUglyWildCardRules();

		List<GameCard> communityCards = [];
		CardSymbol? goodSymbol = null;
		if (isGoodBadUgly)
		{
			communityCards = await context.GameCards
				.Where(c => c.GameId == command.GameId &&
				            c.HandNumber == game.CurrentHandNumber &&
				            c.Location == CardLocation.Community &&
				            !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.ToListAsync(cancellationToken);

			goodSymbol = communityCards
				.FirstOrDefault(c => string.Equals(c.DealtAtPhase, "TheGood", StringComparison.OrdinalIgnoreCase))
				?.Symbol;
		}


		foreach (var gamePlayer in playersInHand)
		{
			if (!playerCardGroups.TryGetValue(gamePlayer.Id,
				    out var cards)) continue; // Skip players without valid hands

			if (isGoodBadUgly)
			{
				var playerOwnedCards = cards
					.Where(c => !c.IsDiscarded)
					.ToList();

				// Include community cards so the response contains all cards used in evaluation
				// and BestCardIndexes can correctly reference into the combined list
				var allGameCards = playerOwnedCards
					.Concat(communityCards)
					.ToList();

				var allCards = allGameCards
					.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol)))
					.ToList();

				int? wildRank = goodSymbol.HasValue ? (int)MapSymbol(goodSymbol.Value) : null;
				var gbuHand = new GoodBadUglyHand(allCards, [], [], wildRank, goodBadUglyWildRules);
				playerHandEvaluations[gamePlayer.Player.Name] = (gbuHand, allGameCards, gamePlayer);
				continue;
			}

			if (cards.Count < 5) continue; // Skip players without valid hands

			// Categorize cards for Seven Card Stud:
			// - Hole cards: Initial face-down cards (ThirdStreet, not visible)
			// - Open cards: Face-up board cards (ThirdStreet through SixthStreet, visible)
			// - Down cards: Final face-down card (SeventhStreet, not visible)
			var holeCards = cards
				.Where(c => c.Location == CardLocation.Hole && c.DealtAtPhase != nameof(Phases.SeventhStreet))
				.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol)))
				.ToList();

			var openCards = cards
				.Where(c => c.Location == CardLocation.Board)
				.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol)))
				.ToList();

			var downCards = cards
				.Where(c => c.Location == CardLocation.Hole && c.DealtAtPhase == nameof(Phases.SeventhStreet))
				.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol)))
				.ToList();

			var studHand = new StudHand(holeCards, openCards, downCards);
			playerHandEvaluations[gamePlayer.Player.Name] = (studHand, cards, gamePlayer);
		}

		if (playerHandEvaluations.Count == 0)
			return new PerformShowdownError
			{
				Message = "No eligible winning hand could be determined at showdown.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};

		var strengthByGamePlayerId = playerHandEvaluations.ToDictionary(
			kvp => kvp.Value.gamePlayer.Id,
			kvp => kvp.Value.hand.Strength);
		var anyLivePlayer = isGoodBadUgly && playerHandEvaluations.Values.Any(evaluation =>
			!string.Equals(evaluation.gamePlayer.VariantState, "UGLY_ELIMINATED", StringComparison.OrdinalIgnoreCase));
		var awarded = await new ContributionLayerAwarder(context).AwardAsync(
			game,
			currentHandPots,
			now,
			isAlreadyAwarded,
			strengthByGamePlayerId,
			gamePlayer => !anyLivePlayer || !string.Equals(
				gamePlayer.VariantState, "UGLY_ELIMINATED", StringComparison.OrdinalIgnoreCase),
			gamePlayerId => playerHandEvaluations.Values
				.First(evaluation => evaluation.gamePlayer.Id == gamePlayerId).hand.Type.ToString(),
			cancellationToken);
		if (awarded.IsT1)
			return new PerformShowdownError
			{
				Message = awarded.AsT1,
				Code = PerformShowdownErrorCode.InvalidGameState
			};

		var payouts = awarded.AsT0.Payouts;
		var winners = awarded.AsT0.WinnerNames.ToList();
		var winReason = awarded.AsT0.WinReason;

		if (!isAlreadyAwarded)
		{
			game.CurrentPhase = nameof(Phases.Complete);
			game.UpdatedAt = now;
			game.HandCompletedAt = now;
			game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
			MoveDealer(game);

			// Settle hand results to cashier ledger
			await handSettlementService.SettleHandAsync(game, payouts, cancellationToken);

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
				awarded.AsT0.HandHistoryPot,
				false,
				winnerInfos,
				winners,
				winReason,
				playerHandEvaluations,
				cancellationToken);
		}

		// 13. Build response
		var playerHandsList = playerHandEvaluations.Select(kvp =>
		{
			var isWinner = winners.Contains(kvp.Key);
			var eliminatedByUgly = isGoodBadUgly && string.Equals(
				kvp.Value.gamePlayer.VariantState, "UGLY_ELIMINATED", StringComparison.OrdinalIgnoreCase);
			usersByEmail.TryGetValue(kvp.Value.gamePlayer.Player.Email ?? string.Empty, out var user);

			// Get the best 5-card hand and find their indexes in the original card list
			var bestHand = kvp.Value.hand is GoodBadUglyHand goodBadUglyHand
				? goodBadUglyHand.BestHandSourceCards
				: kvp.Value.hand.GetBestHand();
			var cardsList = kvp.Value.cards.ToList();
			var bestCardIndexes = new List<int>();
			var usedIndexes = new HashSet<int>();

			foreach (var bestCard in bestHand)
				for (var i = 0; i < cardsList.Count; i++)
					if (!usedIndexes.Contains(i) &&
					    cardsList[i].Suit == (CardSuit)bestCard.Suit &&
					    cardsList[i].Symbol == (CardSymbol)bestCard.Symbol)
					{
						bestCardIndexes.Add(i);
						usedIndexes.Add(i);
						break;
					}

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
				HandDescription = eliminatedByUgly
					? "Dead Hand (The Ugly)"
					: HandDescriptionFormatter.GetHandDescription(kvp.Value.hand),
				HandStrength = kvp.Value.hand.Strength,
				IsWinner = isWinner,
				AmountWon = payouts.GetValueOrDefault(kvp.Key, 0),
				BestCardIndexes = bestCardIndexes,
				IsEliminatedByUgly = eliminatedByUgly
			};
		}).OrderByDescending(h => h.HandStrength ?? 0).ToList();

		return new PerformShowdownSuccessful
		{
			GameId = game.Id,
			WonByFold = false,
			CurrentPhase = game.CurrentPhase,
			Payouts = payouts,
			PlayerHands = playerHandsList
		};
	}

	/// <summary>
	///     Moves the dealer button to the next occupied seat position (clockwise). Skips empty seats but allows sitting-out
	///     players to hold the button.
	/// </summary>
	private static void MoveDealer(Game game)
	{
		var occupiedSeats = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.Select(gp => gp.SeatPosition)
			.ToList();

		if (occupiedSeats.Count == 0) return;

		var currentPosition = game.DealerPosition;

		// Find next occupied seat clockwise from current position
		// Look for seats with higher position numbers first
		var seatsAfterCurrent = occupiedSeats.Where(pos => pos > currentPosition).ToList();

		// Found a seat after the current dealer position
		game.DealerPosition = seatsAfterCurrent.Count > 0
			? seatsAfterCurrent.First()
			:
			// No seats after current position, wrap around to first occupied seat
			occupiedSeats.First();
	}

	/// <summary>
	///     Maps entity CardSuit to core library Suit.
	/// </summary>
	private static Suit MapSuit(CardSuit suit)
	{
		return suit switch
		{
			CardSuit.Hearts => Suit.Hearts,
			CardSuit.Diamonds => Suit.Diamonds,
			CardSuit.Spades => Suit.Spades,
			CardSuit.Clubs => Suit.Clubs,
			_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit")
		};
	}

	/// <summary>
	///     Maps entity CardSymbol to core library Symbol.
	/// </summary>
	private static Symbol MapSymbol(CardSymbol symbol)
	{
		return symbol switch
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
	}

	/// <summary>
	///     Records hand history asynchronously after a hand completes.
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
		Dictionary<string, (StudHand hand, List<GameCard> cards, GamePlayer gamePlayer)>? playerHandEvaluations,
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

			// Get cards for this player if they reached showdown
			List<string>? showdownCards = null;
			var reachedShowdown = !gp.HasFolded && !wonByFold;
			if (reachedShowdown && playerHandEvaluations != null &&
			    playerHandEvaluations.TryGetValue(gp.Player.Name, out var handEvaluation))
			{
				// Get the best 5-card hand from the 7 cards
				var bestHand = handEvaluation.hand is GoodBadUglyHand goodBadUglyHand
					? goodBadUglyHand.BestHandSourceCards
					: handEvaluation.hand.GetBestHand();
				showdownCards = bestHand
					.Select(c => FormatCard((CardSymbol)c.Symbol, (CardSuit)c.Suit))
					.ToList();
			}

			return new PlayerResultInfo
			{
				PlayerId = gp.PlayerId,
				PlayerName = gp.Player.Name,
				SeatPosition = gp.SeatPosition,
				HasFolded = gp.HasFolded,
				ReachedShowdown = reachedShowdown,
				IsWinner = isWinner,
				IsSplitPot = isSplitPot && isWinner,
				NetChipDelta = netDelta,
				WentAllIn = gp.IsAllIn,
				FoldStreet = gp.HasFolded ? "FirstRound" : null, // Simplified for Seven Card Stud
				ShowdownCards = showdownCards
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

	/// <summary>
	///     Formats a card as text (e.g., "3s", "Ah", "Td").
	/// </summary>
	private static string FormatCard(CardSymbol symbol, CardSuit suit)
	{
		var symbolStr = symbol switch
		{
			CardSymbol.Deuce => "2",
			CardSymbol.Three => "3",
			CardSymbol.Four => "4",
			CardSymbol.Five => "5",
			CardSymbol.Six => "6",
			CardSymbol.Seven => "7",
			CardSymbol.Eight => "8",
			CardSymbol.Nine => "9",
			CardSymbol.Ten => "T",
			CardSymbol.Jack => "J",
			CardSymbol.Queen => "Q",
			CardSymbol.King => "K",
			CardSymbol.Ace => "A",
			_ => "?"
		};

		var suitStr = suit switch
		{
			CardSuit.Hearts => "h",
			CardSuit.Diamonds => "d",
			CardSuit.Spades => "s",
			CardSuit.Clubs => "c",
			_ => "?"
		};

		return $"{symbolStr}{suitStr}";
	}
}