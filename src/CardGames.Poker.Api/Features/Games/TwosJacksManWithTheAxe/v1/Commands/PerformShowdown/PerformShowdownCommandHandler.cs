using System.Text.Json;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.Showdown;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.WildCards;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.PerformShowdown;

/// <summary>
///     Handles the <see cref="PerformShowdownCommand" /> to evaluate hands and award pots.
/// </summary>
public class PerformShowdownCommandHandler(
	CardsDbContext context,
	IHandHistoryRecorder handHistoryRecorder,
	IHandSettlementService handSettlementService,
	GameEventsTelemetry? telemetry = null)
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
			.Include(g => g.Pots)
			.ThenInclude(p => p.Contributions)
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

		// 7. Evaluate all hands using wild-aware evaluation
		var playerHandEvaluations =
			new Dictionary<string, (TwosJacksManWithTheAxeDrawHand hand, List<GameCard> cards, GamePlayer gamePlayer,
				List<int> wildIndexes)>();

		foreach (var gamePlayer in playersInHand)
		{
			if (!playerCardGroups.TryGetValue(gamePlayer.Id, out var cards) ||
			    cards.Count < 5) continue; // Skip players without valid hands

			var coreCards = cards.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol))).ToList();
			var wildHand = new TwosJacksManWithTheAxeDrawHand(coreCards);

			// Determine wild card indexes for UI display
			var wildIndexes = new List<int>();
			for (var i = 0; i < coreCards.Count; i++)
				if (TwosJacksManWithTheAxeWildCardRules.IsWild(coreCards[i]))
					wildIndexes.Add(i);

			playerHandEvaluations[gamePlayer.Player.Name] = (wildHand, cards, gamePlayer, wildIndexes);
		}

		// 8. Award each current-hand pot to the players who matched it
		var payouts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		var sevensPayouts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		var highHandPayouts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		var sevensWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var highHandWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var sevensPoolRolledOver = false;
		var potAwards = new Dictionary<Guid, List<PotAward>>();

		foreach (var pot in currentHandPots.OrderBy(p => p.PotOrder))
		{
			if (pot.Amount == 0)
				continue;

			var eligible = EligibleForPot(pot, playerHandEvaluations);
			if (eligible.Count == 0)
				return new PerformShowdownError
				{
					Message = $"Pot {pot.PotOrder} of {pot.Amount} has no eligible winner.",
					Code = PerformShowdownErrorCode.InvalidGameState
				};

			var sevensHere = eligible
				.Where(entry => entry.Value.hand.HasNaturalPairOfSevens())
				.Select(entry => entry.Value.gamePlayer)
				.ToList();
			var maxStrength = eligible.Max(entry => entry.Value.hand.Strength);
			var highHere = eligible
				.Where(entry => entry.Value.hand.Strength == maxStrength)
				.Select(entry => entry.Value.gamePlayer)
				.ToList();

			var sevensHalf = pot.Amount / 2;
			var highHalf = pot.Amount - sevensHalf;
			if (sevensHere.Count == 0)
			{
				highHalf += sevensHalf;
				sevensHalf = 0;
				sevensPoolRolledOver = true;
			}

			var awards = new List<PotAward>();
			if (sevensHalf > 0)
				awards.AddRange(AwardHalf(sevensHere, sevensHalf, game.DealerPosition, isSevens: true));
			if (highHalf > 0)
				awards.AddRange(AwardHalf(highHere, highHalf, game.DealerPosition, isSevens: false));

			awards = awards.Where(award => award.Amount > 0).ToList();

			foreach (var award in awards)
			{
				Add(payouts, award.PlayerName, award.Amount);
				if (award.IsSevens)
				{
					Add(sevensPayouts, award.PlayerName, award.Amount);
					sevensWinners.Add(award.PlayerName);
				}
				else
				{
					Add(highHandPayouts, award.PlayerName, award.Amount);
					highHandWinners.Add(award.PlayerName);
				}
			}

			potAwards[pot.Id] = awards;
		}

		if (payouts.Values.Sum() != currentHandPots.Sum(p => p.Amount))
			return new PerformShowdownError
			{
				Message = "Awarded chips do not equal the current hand's pots.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};

		// 10. Update player chip stacks
		if (!isAlreadyAwarded)
		{
			foreach (var payout in payouts)
			{
				var gamePlayer = playerHandEvaluations[payout.Key].gamePlayer;
				gamePlayer.ChipStack += payout.Value;
			}

			// 11. Mark pots as awarded with that pot's winners only
			foreach (var pot in currentHandPots)
			{
				potAwards.TryGetValue(pot.Id, out var awards);
				awards ??= [];

				var winnerPayoutsList = awards
					.GroupBy(award => award.PlayerName, StringComparer.OrdinalIgnoreCase)
					.Select(group =>
					{
						var name = group.Key;
						var sevensForThisPot = group.Where(award => award.IsSevens).Sum(award => award.Amount);
						var highForThisPot = group.Where(award => !award.IsSevens).Sum(award => award.Amount);
						var gamePlayer = playerHandEvaluations[name].gamePlayer;
						return new
						{
							playerId = gamePlayer.PlayerId.ToString(),
							playerName = name,
							amount = sevensForThisPot + highForThisPot,
							sevensAmount = sevensForThisPot,
							highHandAmount = highForThisPot
						};
					})
					.ToList();

				pot.IsAwarded = true;
				pot.AwardedAt = now;
				pot.WinReason = WinReasonForPot(awards, playerHandEvaluations);
				pot.WinnerPayouts = JsonSerializer.Serialize(winnerPayoutsList);
			}

			// 12. Update game state
			game.CurrentPhase = nameof(Phases.Complete);
			game.UpdatedAt = now;
			game.HandCompletedAt = now;
			game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
			MoveDealer(game);

			await handSettlementService.SettleHandAsync(game, payouts, cancellationToken);
			await context.SaveChangesAsync(cancellationToken);

			// Record hand history for showdown
			var winnerInfos = payouts
				.Where(kvp => kvp.Value > 0)
				.Select(kvp =>
				{
					var gp = playerHandEvaluations[kvp.Key].gamePlayer;
					return (gp.PlayerId, PlayerName: kvp.Key, kvp.Value);
				})
				.ToList();
			var winnerList = winnerInfos.Select(winner => winner.PlayerName).ToList();

			await RecordHandHistoryAsync(
				game,
				game.GamePlayers.ToList(),
				now,
				totalPot,
				false,
				winnerInfos,
				winnerList,
				HandWinReason(sevensWinners, highHandWinners, playerHandEvaluations),
				cancellationToken);
		}

		// 13. Build response with enhanced wild card and split pot info
		var playerHandsList = playerHandEvaluations.Select(kvp =>
		{
			var isSevensWinner = sevensWinners.Contains(kvp.Key);
			var isHighHandWinner = highHandWinners.Contains(kvp.Key);
			var isWinner = isSevensWinner || isHighHandWinner;
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
				HandStrength = kvp.Value.hand.Strength,
				IsWinner = isWinner,
				AmountWon = payouts.GetValueOrDefault(kvp.Key, 0),
				IsSevensWinner = isSevensWinner,
				IsHighHandWinner = isHighHandWinner,
				SevensAmountWon = sevensPayouts.GetValueOrDefault(kvp.Key, 0),
				HighHandAmountWon = highHandPayouts.GetValueOrDefault(kvp.Key, 0),
				WildCardIndexes = kvp.Value.wildIndexes.Count > 0 ? kvp.Value.wildIndexes : null
			};
		}).OrderByDescending(h => h.HandStrength ?? 0).ToList();

		return new PerformShowdownSuccessful
		{
			GameId = game.Id,
			WonByFold = false,
			CurrentPhase = game.CurrentPhase,
			Payouts = payouts,
			PlayerHands = playerHandsList,
			SevensWinners = sevensWinners.ToList(),
			HighHandWinners = highHandWinners.ToList(),
			SevensPoolRolledOver = sevensPoolRolledOver,
			SevensPayouts = sevensPayouts,
			HighHandPayouts = highHandPayouts
		};
	}

	private sealed record PotAward(string PlayerName, int Amount, bool IsSevens);

	private static Dictionary<string, (TwosJacksManWithTheAxeDrawHand hand, List<GameCard> cards, GamePlayer gamePlayer,
		List<int> wildIndexes)> EligibleForPot(
		CardGames.Poker.Api.Data.Entities.Pot pot,
		Dictionary<string, (TwosJacksManWithTheAxeDrawHand hand, List<GameCard> cards, GamePlayer gamePlayer, List<int>
			wildIndexes)> playerHandEvaluations)
	{
		if (pot.Contributions.Count == 0)
			return playerHandEvaluations;

		var eligibleIds = pot.Contributions
			.Where(contribution => contribution.IsEligibleToWin)
			.Select(contribution => contribution.GamePlayerId)
			.ToHashSet();

		return playerHandEvaluations
			.Where(entry => eligibleIds.Contains(entry.Value.gamePlayer.Id))
			.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
	}

	private static List<PotAward> AwardHalf(
		List<GamePlayer> winners,
		int amount,
		int dealerSeat,
		bool isSevens)
	{
		var ordered = OddChipOrder.OrderClockwiseFromDealer(
			winners,
			dealerSeat,
			gamePlayer => gamePlayer.SeatPosition);
		var shares = OddChipOrder.Distribute(amount, ordered);
		var awards = new List<PotAward>(ordered.Count);
		for (var index = 0; index < ordered.Count; index++)
			awards.Add(new PotAward(ordered[index].Player.Name, shares[index], isSevens));
		return awards;
	}

	private static void Add(Dictionary<string, int> target, string name, int amount)
	{
		target[name] = target.GetValueOrDefault(name, 0) + amount;
	}

	private static string? WinReasonForPot(
		List<PotAward> awards,
		Dictionary<string, (TwosJacksManWithTheAxeDrawHand hand, List<GameCard> cards, GamePlayer gamePlayer, List<int>
			wildIndexes)> playerHandEvaluations)
	{
		var sevensNames = awards
			.Where(award => award.IsSevens)
			.Select(award => award.PlayerName)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		var highNames = awards
			.Where(award => !award.IsSevens)
			.Select(award => award.PlayerName)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (sevensNames.Count > 0)
			return $"7s: {string.Join(", ", sevensNames)}; High: {string.Join(", ", highNames)}";

		if (highNames.Count == 1)
			return playerHandEvaluations[highNames[0]].hand.Type.ToString();

		if (highNames.Count > 1)
			return $"Split pot - {playerHandEvaluations[highNames[0]].hand.Type}";

		return null;
	}

	private static string? HandWinReason(
		HashSet<string> sevensWinners,
		HashSet<string> highHandWinners,
		Dictionary<string, (TwosJacksManWithTheAxeDrawHand hand, List<GameCard> cards, GamePlayer gamePlayer, List<int>
			wildIndexes)> playerHandEvaluations)
	{
		if (sevensWinners.Count > 0)
			return $"7s: {string.Join(", ", sevensWinners)}; High: {string.Join(", ", highHandWinners)}";

		if (highHandWinners.Count > 1)
			return $"Split pot - {playerHandEvaluations[highHandWinners.First()].hand.Type}";

		if (highHandWinners.Count == 1)
			return playerHandEvaluations[highHandWinners.First()].hand.Type.ToString();

		return null;
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
				FoldStreet = gp.HasFolded ? "FirstRound" : null // Simplified for Twos/Jacks/Axe
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