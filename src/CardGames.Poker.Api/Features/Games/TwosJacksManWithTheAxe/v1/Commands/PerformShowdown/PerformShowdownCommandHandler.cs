using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Games.TwosJacksManWithTheAxe;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.WildCards;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.PerformShowdown;

/// <summary>
/// Handles the <see cref="PerformShowdownCommand"/> to evaluate hands and award pots.
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

		// 2. Validate game is in showdown phase
		if (game.CurrentPhase != nameof(TwosJacksManWithTheAxePhase.Showdown))
		{
			return new PerformShowdownError
			{
				Message = $"Cannot perform showdown. Game is in '{game.CurrentPhase}' phase. " +
				          $"Showdown can only be performed when the game is in '{nameof(TwosJacksManWithTheAxePhase.Showdown)}' phase.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};
		}

		// 3. Get players who have not folded
		var playersInHand = game.GamePlayers
			.Where(gp => !gp.HasFolded && gp.Status == GamePlayerStatus.Active)
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
			winner.ChipStack += totalPot;

			// Mark pots as awarded
			foreach (var pot in currentHandPots)
			{
				pot.IsAwarded = true;
				pot.AwardedAt = now;
				pot.WinReason = "All others folded";
			}

			game.CurrentPhase = nameof(TwosJacksManWithTheAxePhase.Complete);
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

			// 7. Evaluate all hands using wild-aware evaluation
			var playerHandEvaluations = new Dictionary<string, (TwosJacksManWithTheAxeDrawHand hand, List<GameCard> cards, GamePlayer gamePlayer, List<int> wildIndexes)>();

		foreach (var gamePlayer in playersInHand)
		{
			if (!playerCardGroups.TryGetValue(gamePlayer.Id, out var cards) || cards.Count < 5)
			{
				continue; // Skip players without valid hands
			}

			var coreCards = cards.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol))).ToList();
			var wildHand = new TwosJacksManWithTheAxeDrawHand(coreCards);
			
			// Determine wild card indexes for UI display
			var wildIndexes = new List<int>();
			for (int i = 0; i < coreCards.Count; i++)
			{
				if (TwosJacksManWithTheAxeWildCardRules.IsWild(coreCards[i]))
				{
					wildIndexes.Add(i);
				}
			}
			
			playerHandEvaluations[gamePlayer.Player.Name] = (wildHand, cards, gamePlayer, wildIndexes);
		}

		// 8. Build ordered player list for deterministic remainder distribution (dealer-left first)
		var orderedPlayerNames = new List<string>();
		for (int i = 1; i <= game.GamePlayers.Count; i++)
		{
			var idx = (game.DealerPosition + i) % game.GamePlayers.Count;
			var player = game.GamePlayers.OrderBy(gp => gp.SeatPosition).Skip(idx).FirstOrDefault()
			             ?? game.GamePlayers.OrderBy(gp => gp.SeatPosition).First();
			if (!player.HasFolded && playerHandEvaluations.ContainsKey(player.Player.Name))
			{
				orderedPlayerNames.Add(player.Player.Name);
			}
		}

		// 9. Apply 7s half-pot + high hand half-pot logic
		var sevensWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var highHandWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var sevensPoolRolledOver = false;

		// Determine sevens winners (natural pair of 7s)
		foreach (var kvp in playerHandEvaluations)
		{
			if (kvp.Value.hand.HasNaturalPairOfSevens())
			{
				sevensWinners.Add(kvp.Key);
			}
		}

		// Determine high hand winners
		if (playerHandEvaluations.Count > 0)
		{
			var maxStrength = playerHandEvaluations.Values.Max(h => h.hand.Strength);
			foreach (var kvp in playerHandEvaluations.Where(k => k.Value.hand.Strength == maxStrength))
			{
				highHandWinners.Add(kvp.Key);
			}
		}

		// Calculate payouts using 7s split pot logic
		var sevensPool = totalPot / 2;
		var handPool = totalPot - sevensPool;

		// If no sevens winners, roll sevens pool into hand pool
		if (sevensWinners.Count == 0)
		{
			handPool += sevensPool;
			sevensPool = 0;
			sevensPoolRolledOver = true;
		}

		var payouts = new Dictionary<string, int>();
		var sevensPayouts = new Dictionary<string, int>();
		var highHandPayouts = new Dictionary<string, int>();

		// Award sevens pool
		if (sevensPool > 0 && sevensWinners.Count > 0)
		{
			var sevensWinnersList = orderedPlayerNames.Where(n => sevensWinners.Contains(n)).ToList();
			var share = sevensPool / sevensWinnersList.Count;
			var remainder = sevensPool % sevensWinnersList.Count;

			foreach (var winner in sevensWinnersList)
			{
				var payout = share;
				if (remainder > 0)
				{
					payout++;
					remainder--;
				}
				sevensPayouts[winner] = payout;
				payouts[winner] = payouts.GetValueOrDefault(winner, 0) + payout;
			}
		}

		// Award high hand pool
		if (handPool > 0 && highHandWinners.Count > 0)
		{
			var highHandWinnersList = orderedPlayerNames.Where(n => highHandWinners.Contains(n)).ToList();
			var share = handPool / highHandWinnersList.Count;
			var remainder = handPool % highHandWinnersList.Count;

			foreach (var winner in highHandWinnersList)
			{
				var payout = share;
				if (remainder > 0)
				{
					payout++;
					remainder--;
				}
				highHandPayouts[winner] = payout;
				payouts[winner] = payouts.GetValueOrDefault(winner, 0) + payout;
			}
		}

		// 10. Update player chip stacks
		foreach (var payout in payouts)
		{
			var gamePlayer = playerHandEvaluations[payout.Key].gamePlayer;
			gamePlayer.ChipStack += payout.Value;
		}

		// 11. Mark pots as awarded
		var winnerList = payouts.Keys.ToList();
		var winReason = winnerList.Count > 1
			? $"Split pot - {playerHandEvaluations[winnerList[0]].hand.Type}"
			: playerHandEvaluations[winnerList[0]].hand.Type.ToString();

		if (sevensWinners.Count > 0)
		{
			winReason = $"7s: {string.Join(", ", sevensWinners)}; High: {string.Join(", ", highHandWinners)}";
		}

		foreach (var pot in currentHandPots)
		{
			pot.IsAwarded = true;
			pot.AwardedAt = now;
				pot.WinReason = winReason;
			}

			// 12. Update game state
			game.CurrentPhase = nameof(TwosJacksManWithTheAxePhase.Complete);
			game.UpdatedAt = now;
			game.HandCompletedAt = now;
			game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
			MoveDealer(game);

			await context.SaveChangesAsync(cancellationToken);

			// Record hand history for showdown
			var winnerInfos = payouts.Select(kvp =>
			{
				var gp = playerHandEvaluations[kvp.Key].gamePlayer;
				return (gp.PlayerId, kvp.Key, kvp.Value);
			}).ToList();

			await RecordHandHistoryAsync(
				game,
				game.GamePlayers.ToList(),
				now,
				totalPot,
				wonByFold: false,
				winners: winnerInfos,
				winnerNames: winnerList,
				winningHandDescription: winReason,
				cancellationToken);

				// 13. Build response with enhanced wild card and split pot info
				var playerHandsList = playerHandEvaluations.Select(kvp =>
				{
					var isSevensWinner = sevensWinners.Contains(kvp.Key);
					var isHighHandWinner = highHandWinners.Contains(kvp.Key);
					var isWinner = isSevensWinner || isHighHandWinner;
					usersByEmail.TryGetValue(kvp.Value.gamePlayer.Player.Email ?? string.Empty, out var user);
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

			/// <summary>
			/// Moves the dealer button to the next occupied seat position (clockwise).
			/// Skips empty seats but allows sitting-out players to hold the button.
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
				// Look for seats with higher position numbers first
				var seatsAfterCurrent = occupiedSeats.Where(pos => pos > currentPosition).ToList();

				if (seatsAfterCurrent.Count > 0)
				{
					// Found a seat after the current dealer position
					game.DealerPosition = seatsAfterCurrent.First();
				}
				else
				{
					// No seats after current position, wrap around to first occupied seat
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

