using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

using CardGames.Poker.Api.Services.InMemoryEngine;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;

/// <summary>
/// Handles the <see cref="PerformShowdownCommand"/> to evaluate Hold'Em hands and award pots.
/// Hold'Em hand evaluation uses 2 hole cards + 5 community cards → best 5-of-7.
/// </summary>
public class PerformShowdownCommandHandler(CardsDbContext context,
	IOptions<InMemoryEngineOptions> engineOptions,
	IGameStateManager gameStateManager, IHandHistoryRecorder handHistoryRecorder, IHandSettlementService handSettlementService)
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
		{
			return new PerformShowdownError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = PerformShowdownErrorCode.GameNotFound
			};
		}

		// Filter pots to current hand
		var currentHandPots = game.Pots.Where(p => p.HandNumber == game.CurrentHandNumber).ToList();
		var isAlreadyAwarded = currentHandPots.Any(p => p.IsAwarded);

		// 2. Validate game is in showdown phase
		if (game.CurrentPhase != "Showdown" && !isAlreadyAwarded)
		{
			return new PerformShowdownError
			{
				Message = $"Cannot perform showdown. Game is in '{game.CurrentPhase}' phase. " +
						  "Showdown can only be performed when the game is in 'Showdown' phase.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};
		}

		// 3. Get players who have not folded
		var playersInHand = game.GamePlayers
			.Where(gp => !gp.HasFolded && (gp.Status == GamePlayerStatus.Active || gp.IsAllIn))
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

		// 4. Load hole cards for players in hand (each player's own cards)
		var playerCards = await context.GameCards
			.Where(c => c.GameId == command.GameId &&
						c.HandNumber == game.CurrentHandNumber &&
						!c.IsDiscarded &&
						c.GamePlayerId != null &&
						playersInHand.Select(p => p.Id).Contains(c.GamePlayerId.Value))
			.ToListAsync(cancellationToken);

		var playerCardGroups = playerCards
			.GroupBy(c => c.GamePlayerId!.Value)
			.ToDictionary(g => g.Key, g => g.OrderBy(c => c.DealOrder).ToList());

		// 5. Load community cards (shared board — GamePlayerId is null, Location is Community)
		var communityCards = await context.GameCards
			.Where(c => c.GameId == command.GameId &&
						c.HandNumber == game.CurrentHandNumber &&
						!c.IsDiscarded &&
						c.GamePlayerId == null &&
						c.Location == CardLocation.Community)
			.OrderBy(c => c.DealOrder)
			.ToListAsync(cancellationToken);

		// 6. Calculate total pot
		var totalPot = currentHandPots.Sum(p => p.Amount);

		// 7. Handle win by fold (only one player remaining)
		if (playersInHand.Count == 1)
		{
			var winner = playersInHand[0];

			if (!isAlreadyAwarded)
			{
				winner.ChipStack += totalPot;

				foreach (var pot in currentHandPots)
				{
					pot.IsAwarded = true;
					pot.AwardedAt = now;
					pot.WinReason = "All others folded";

					var winnerPayoutsList = new[] { new { playerId = winner.PlayerId.ToString(), playerName = winner.Player.Name, amount = totalPot } };
					pot.WinnerPayouts = System.Text.Json.JsonSerializer.Serialize(winnerPayoutsList);
				}

				game.CurrentPhase = "Complete";
				game.UpdatedAt = now;
				game.HandCompletedAt = now;
				game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
				MoveDealer(game);

				// Settle win-by-fold to cashier ledger
				var foldPayouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } };
				await handSettlementService.SettleHandAsync(game, foldPayouts, cancellationToken);

				await context.SaveChangesAsync(cancellationToken);

				if (engineOptions.Value.Enabled)
					await gameStateManager.ReloadGameAsync(command.GameId, cancellationToken);

				await RecordHandHistoryAsync(
					game,
					game.GamePlayers.ToList(),
					now,
					totalPot,
					wonByFold: true,
					winners: [(winner.PlayerId, winner.Player.Name, totalPot)],
					winnerNames: [winner.Player.Name],
					winningHandDescription: null,
					playerHandEvaluations: null,
					cancellationToken);
			}

			var winnerCards = playerCardGroups.GetValueOrDefault(winner.Id, []);
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

		// 8. Build community Card objects for evaluation
		var communityCoreCards = communityCards
			.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol)))
			.ToList();

		// 9. Evaluate all hands using HoldemHand (best 5 of 7: 2 hole + 5 community)
		var playerHandEvaluations = new Dictionary<string, (HoldemHand hand, List<GameCard> allCards, GamePlayer gamePlayer)>();

		foreach (var gamePlayer in playersInHand)
		{
			if (!playerCardGroups.TryGetValue(gamePlayer.Id, out var holeCardEntities))
			{
				continue;
			}

			if (holeCardEntities.Count < 2)
			{
				continue;
			}

			var holeCoreCards = holeCardEntities
				.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol)))
				.ToList();

			var holdemHand = new HoldemHand(holeCoreCards, communityCoreCards);

			// Build the full card list for display: hole cards first, then community cards
			var allDisplayCards = holeCardEntities.ToList();
			allDisplayCards.AddRange(communityCards);

			playerHandEvaluations[gamePlayer.Player.Name] = (holdemHand, allDisplayCards, gamePlayer);
		}

		if (playerHandEvaluations.Count == 0)
		{
			return new PerformShowdownError
			{
				Message = "No eligible hands could be evaluated at showdown.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};
		}

		// 10. Determine winners (highest strength)
		var maxStrength = playerHandEvaluations.Values.Max(h => h.hand.Strength);
		var winners = playerHandEvaluations
			.Where(kvp => kvp.Value.hand.Strength == maxStrength)
			.Select(kvp => kvp.Key)
			.ToList();

		if (winners.Count == 0)
		{
			return new PerformShowdownError
			{
				Message = "No eligible winning hand could be determined at showdown.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};
		}

		// 11. Calculate payouts (split pot if multiple winners)
		var payoutPerWinner = totalPot / winners.Count;
		var remainder = totalPot % winners.Count;
		var payouts = new Dictionary<string, int>();

		foreach (var winner in winners)
		{
			payouts[winner] = payoutPerWinner;
		}

		if (remainder > 0 && winners.Count > 0)
		{
			payouts[winners[0]] += remainder;
		}

		// 12. Update player chip stacks and mark pots as awarded
		if (!isAlreadyAwarded)
		{
			foreach (var payout in payouts)
			{
				var gamePlayer = playerHandEvaluations[payout.Key].gamePlayer;
				gamePlayer.ChipStack += payout.Value;
			}

			var winReason = winners.Count > 1
				? $"Split pot - {playerHandEvaluations[winners[0]].hand.Type}"
				: playerHandEvaluations[winners[0]].hand.Type.ToString();

			var winnerPayoutsList = payouts.Select(p =>
			{
				var gp = playerHandEvaluations[p.Key].gamePlayer;
				return new { playerId = gp.PlayerId.ToString(), playerName = p.Key, amount = p.Value };
			}).ToList();
			var winnerPayoutsJson = System.Text.Json.JsonSerializer.Serialize(winnerPayoutsList);

			foreach (var pot in currentHandPots)
			{
				pot.IsAwarded = true;
				pot.AwardedAt = now;
				pot.WinReason = winReason;
				pot.WinnerPayouts = winnerPayoutsJson;
			}

			game.CurrentPhase = "Complete";
			game.UpdatedAt = now;
			game.HandCompletedAt = now;
			game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
			MoveDealer(game);

			// Settle hand results to cashier ledger
			await handSettlementService.SettleHandAsync(game, payouts, cancellationToken);

			await context.SaveChangesAsync(cancellationToken);

			if (engineOptions.Value.Enabled)
				await gameStateManager.ReloadGameAsync(command.GameId, cancellationToken);

			// Record hand history
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
				playerHandEvaluations: playerHandEvaluations,
				cancellationToken);
		}

		// 13. Build response
		var playerHandsList = playerHandEvaluations.Select(kvp =>
		{
			var isWinner = winners.Contains(kvp.Key);
			usersByEmail.TryGetValue(kvp.Value.gamePlayer.Player.Email ?? string.Empty, out var user);

			// Get the best 5-card hand and find their indexes in the full card list (hole + community)
			var bestHand = FindBestFiveCardHand(kvp.Value.hand);
			var cardsList = kvp.Value.allCards;
			var bestCardIndexes = new List<int>();
			var usedIndexes = new HashSet<int>();

			foreach (var bestCard in bestHand)
			{
				for (var i = 0; i < cardsList.Count; i++)
				{
					if (!usedIndexes.Contains(i) &&
						cardsList[i].Suit == (CardSuit)bestCard.Suit &&
						cardsList[i].Symbol == (CardSymbol)bestCard.Symbol)
					{
						bestCardIndexes.Add(i);
						usedIndexes.Add(i);
						break;
					}
				}
			}

			return new ShowdownPlayerHand
			{
				PlayerName = kvp.Key,
				PlayerFirstName = user?.FirstName,
				Cards = kvp.Value.allCards.Select(c => new ShowdownCard
				{
					Suit = c.Suit,
					Symbol = c.Symbol
				}).ToList(),
				HandType = kvp.Value.hand.Type.ToString(),
				HandStrength = kvp.Value.hand.Strength,
				IsWinner = isWinner,
				AmountWon = payouts.GetValueOrDefault(kvp.Key, 0),
				BestCardIndexes = bestCardIndexes
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

	private static Suit MapSuit(CardSuit suit) => suit switch
	{
		CardSuit.Hearts => Suit.Hearts,
		CardSuit.Diamonds => Suit.Diamonds,
		CardSuit.Spades => Suit.Spades,
		CardSuit.Clubs => Suit.Clubs,
		_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit")
	};

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
	/// Records hand history asynchronously after a hand completes.
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
		Dictionary<string, (HoldemHand hand, List<GameCard> allCards, GamePlayer gamePlayer)>? playerHandEvaluations,
		CancellationToken cancellationToken)
	{
		var isSplitPot = winners.Count > 1;
		var winnerNameSet = winnerNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var playerResults = allPlayers.Select(gp =>
		{
			var isWinner = winnerNameSet.Contains(gp.Player.Name);
			var netDelta = isWinner
				? winners.First(w => w.PlayerId == gp.PlayerId).AmountWon - gp.TotalContributedThisHand
				: -gp.TotalContributedThisHand;

			List<string>? showdownCards = null;
			var reachedShowdown = !gp.HasFolded && !wonByFold;
			if (reachedShowdown && playerHandEvaluations != null &&
				playerHandEvaluations.TryGetValue(gp.Player.Name, out var handEvaluation))
			{
				var bestHand = FindBestFiveCardHand(handEvaluation.hand);
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
				FoldStreet = gp.HasFolded ? "PreFlop" : null,
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
	/// Finds the best 5-card hand from a HoldemHand by evaluating all C(7,5) combinations.
	/// </summary>
	private static List<Card> FindBestFiveCardHand(HoldemHand hand)
	{
		var allCards = hand.HoleCards.Concat(hand.CommunityCards).ToList();

		if (allCards.Count <= 5)
		{
			return allCards;
		}

		var ranking = HandTypeStrengthRanking.Classic;
		List<Card>? bestCombo = null;
		long bestStrength = long.MinValue;

		foreach (var combo in allCards.SubsetsOfSize(5))
		{
			var comboList = combo.ToList();
			var handType = HandTypeDetermination.DetermineHandType(comboList);
			var strength = HandStrength.Calculate(comboList, handType, ranking);
			if (strength > bestStrength)
			{
				bestStrength = strength;
				bestCombo = comboList;
			}
		}

		return bestCombo ?? allCards.Take(5).ToList();
	}

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
