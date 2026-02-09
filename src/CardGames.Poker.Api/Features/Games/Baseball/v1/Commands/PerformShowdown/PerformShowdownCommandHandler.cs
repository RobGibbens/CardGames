using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
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

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.PerformShowdown;

public class PerformShowdownCommandHandler(CardsDbContext context, IHandHistoryRecorder handHistoryRecorder)
	: IRequestHandler<PerformShowdownCommand, OneOf<PerformShowdownSuccessful, PerformShowdownError>>
{
	public async Task<OneOf<PerformShowdownSuccessful, PerformShowdownError>> Handle(
		PerformShowdownCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
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

		var currentHandPots = game.Pots.Where(p => p.HandNumber == game.CurrentHandNumber).ToList();
		var isAlreadyAwarded = currentHandPots.Any(p => p.IsAwarded);

		if (game.CurrentPhase != nameof(Phases.Showdown) && !isAlreadyAwarded)
		{
			return new PerformShowdownError
			{
				Message = $"Cannot perform showdown. Game is in '{game.CurrentPhase}' phase. " +
						  $"Showdown can only be performed when the game is in '{nameof(Phases.Showdown)}' phase.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};
		}

		var playersInHand = game.GamePlayers
			.Where(gp => !gp.HasFolded && (gp.Status == GamePlayerStatus.Active || gp.IsAllIn))
			.ToList();

		var playerEmails = game.GamePlayers
			.Where(gp => gp.Player.Email != null)
			.Select(gp => gp.Player.Email!)
			.ToList();

		var usersByEmail = await context.Users
			.AsNoTracking()
			.Where(u => u.Email != null && playerEmails.Contains(u.Email))
			.Select(u => new { Email = u.Email!, u.FirstName })
			.ToDictionaryAsync(u => u.Email, StringComparer.OrdinalIgnoreCase, cancellationToken);

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

		var totalPot = currentHandPots.Sum(p => p.Amount);

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

				game.CurrentPhase = nameof(Phases.Complete);
				game.UpdatedAt = now;
				game.HandCompletedAt = now;
				game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
				MoveDealer(game);

				await context.SaveChangesAsync(cancellationToken);

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

		var playerHandEvaluations = new Dictionary<string, (BaseballHand hand, List<GameCard> cards, GamePlayer gamePlayer, List<int> wildIndexes)>();
		var wildCardRules = new BaseballWildCardRules();

		foreach (var gamePlayer in playersInHand)
		{
			if (!playerCardGroups.TryGetValue(gamePlayer.Id, out var cards) || cards.Count < 5)
			{
				continue;
			}

			var holeCards = cards
				.Where(c => c.Location == CardLocation.Hole)
				.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol)))
				.ToList();

			var openCards = cards
				.Where(c => c.Location == CardLocation.Board)
				.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol)))
				.ToList();

			var baseballHand = new BaseballHand(holeCards, openCards, []);
			var wildIndexes = new List<int>();
			var coreCards = baseballHand.Cards.ToList();

			var wildCards = wildCardRules.DetermineWildCards(coreCards);
			for (var i = 0; i < coreCards.Count; i++)
			{
				if (wildCards.Contains(coreCards[i]))
				{
					wildIndexes.Add(i);
				}
			}

			playerHandEvaluations[gamePlayer.Player.Name] = (baseballHand, cards, gamePlayer, wildIndexes);
		}

		var maxStrength = playerHandEvaluations.Values.Max(h => h.hand.Strength);
		var winners = playerHandEvaluations
			.Where(kvp => kvp.Value.hand.Strength == maxStrength)
			.Select(kvp => kvp.Key)
			.ToList();

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

			game.CurrentPhase = nameof(Phases.Complete);
			game.UpdatedAt = now;
			game.HandCompletedAt = now;
			game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
			MoveDealer(game);

			await context.SaveChangesAsync(cancellationToken);

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
				playerHandEvaluations: playerHandEvaluations.ToDictionary(
					k => k.Key,
					v => (StudHand)v.Value.hand,
					StringComparer.OrdinalIgnoreCase),
				cancellationToken);
		}

		var playerHandsList = playerHandEvaluations.Select(kvp =>
		{
			var isWinner = winners.Contains(kvp.Key);
			usersByEmail.TryGetValue(kvp.Value.gamePlayer.Player.Email ?? string.Empty, out var user);

			var bestHandSourceCards = kvp.Value.hand.BestHandSourceCards;
			var playerGameCards = kvp.Value.cards;
			var bestCardIndexes = new List<int>();
			var usedIndices = new HashSet<int>();

			foreach (var bestCard in bestHandSourceCards)
			{
				var index = -1;
				for (var i = 0; i < playerGameCards.Count; i++)
				{
					if (usedIndices.Contains(i)) continue;

					var gc = playerGameCards[i];
					if (MapSuit(gc.Suit) == bestCard.Suit && MapSymbol(gc.Symbol) == bestCard.Symbol)
					{
						index = i;
						break;
					}
				}

				if (index != -1)
				{
					bestCardIndexes.Add(index);
					usedIndices.Add(index);
				}
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
				HandDescription = HandDescriptionFormatter.GetHandDescription(kvp.Value.hand),
				HandStrength = kvp.Value.hand.Strength,
				IsWinner = isWinner,
				AmountWon = payouts.GetValueOrDefault(kvp.Key, 0),
				WildCardIndexes = kvp.Value.wildIndexes,
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

		game.DealerPosition = seatsAfterCurrent.Count > 0
			? seatsAfterCurrent.First()
			: occupiedSeats.First();
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

	private static string FormatCard(CardSymbol symbol, CardSuit suit)
	{
		var rank = symbol switch
		{
			CardSymbol.Ace => "A",
			CardSymbol.King => "K",
			CardSymbol.Queen => "Q",
			CardSymbol.Jack => "J",
			CardSymbol.Ten => "T",
			CardSymbol.Nine => "9",
			CardSymbol.Eight => "8",
			CardSymbol.Seven => "7",
			CardSymbol.Six => "6",
			CardSymbol.Five => "5",
			CardSymbol.Four => "4",
			CardSymbol.Three => "3",
			CardSymbol.Deuce => "2",
			_ => "?"
		};

		var suitChar = suit switch
		{
			CardSuit.Clubs => "c",
			CardSuit.Diamonds => "d",
			CardSuit.Hearts => "h",
			CardSuit.Spades => "s",
			_ => "?"
		};

		return $"{rank}{suitChar}";
	}

	private async Task RecordHandHistoryAsync(
		Game game,
		List<GamePlayer> allPlayers,
		DateTimeOffset completedAt,
		int totalPot,
		bool wonByFold,
		List<(Guid PlayerId, string PlayerName, int AmountWon)> winners,
		List<string> winnerNames,
		string? winningHandDescription,
		Dictionary<string, StudHand>? playerHandEvaluations,
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
				var bestHand = handEvaluation.GetBestHand();
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
				FoldStreet = gp.HasFolded ? "FirstRound" : null,
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
}
