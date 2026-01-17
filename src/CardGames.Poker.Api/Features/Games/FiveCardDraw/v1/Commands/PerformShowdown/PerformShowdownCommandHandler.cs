using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Hands.DrawHands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.PerformShowdown;

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

		// 1. Load the game with its players, cards, and pots (including contributions for eligibility)
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.Pots)
				.ThenInclude(p => p.Contributions)
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
		var isAlreadyAwarded = currentHandPots.Any(p => p.IsAwarded);

		// 2. Validate game is in showdown phase
		if (game.CurrentPhase != nameof(Phases.Showdown) && !isAlreadyAwarded)
		{
			return new PerformShowdownError
			{
				Message = $"Cannot perform showdown. Game is in '{game.CurrentPhase}' phase. " +
						  $"Showdown can only be performed when the game is in '{nameof(Phases.Showdown)}' phase.",
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
			
			if (!isAlreadyAwarded)
			{
				winner.ChipStack += totalPot;

				// Mark pots as awarded
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
				UpdateSitOutStatus(game);
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

			// 7. Evaluate all hands
			var playerHandEvaluations = new Dictionary<string, (DrawHand hand, List<GameCard> cards, GamePlayer gamePlayer)>();


			foreach (var gamePlayer in playersInHand)
			{
				if (!playerCardGroups.TryGetValue(gamePlayer.Id, out var cards) || cards.Count < 5)
				{
					continue; // Skip players without valid hands
				}

				var coreCards = cards.Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol))).ToList();
				var drawHand = new DrawHand(coreCards);
				playerHandEvaluations[gamePlayer.Player.Name] = (drawHand, cards, gamePlayer);
			}

			// 8. Award each pot to the best hand among ELIGIBLE players only
			var payouts = new Dictionary<string, int>();
			var allWinners = new HashSet<string>();
			string? overallWinReason = null;

			if (!isAlreadyAwarded)
			{
				// Order pots by PotOrder (main pot first, then side pots)
				var orderedPots = currentHandPots.OrderBy(p => p.PotOrder).ToList();

				foreach (var pot in orderedPots)
				{
					if (pot.Amount == 0)
					{
						continue;
					}

					// Get eligible players for this pot from contributions
					var eligiblePlayerIds = pot.Contributions
						.Where(c => c.IsEligibleToWin)
						.Select(c => c.GamePlayerId)
						.ToHashSet();

					// If no contribution records exist, fall back to all players in hand
					// (handles legacy data or pots created before side pot calculation)
					if (eligiblePlayerIds.Count == 0)
					{
						eligiblePlayerIds = playersInHand.Select(p => p.Id).ToHashSet();
					}

					// Filter hand evaluations to only eligible players
					var eligibleHands = playerHandEvaluations
						.Where(kvp => eligiblePlayerIds.Contains(kvp.Value.gamePlayer.Id))
						.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

					if (eligibleHands.Count == 0)
					{
						continue; // No eligible players for this pot (shouldn't happen)
					}

					// Determine winner(s) for this pot
					var maxStrength = eligibleHands.Values.Max(h => h.hand.Strength);
					var potWinners = eligibleHands
						.Where(kvp => kvp.Value.hand.Strength == maxStrength)
						.Select(kvp => kvp.Key)
						.ToList();

					// Calculate payouts for this pot
					var potPayoutPerWinner = pot.Amount / potWinners.Count;
					var potRemainder = pot.Amount % potWinners.Count;

					var potPayoutsList = new List<object>();
					foreach (var winner in potWinners)
					{
						var payout = potPayoutPerWinner;
						if (potRemainder > 0)
						{
							payout++;
							potRemainder--;
						}

						// Add to total payouts
						if (payouts.TryGetValue(winner, out var existingPayout))
						{
							payouts[winner] = existingPayout + payout;
						}
						else
						{
							payouts[winner] = payout;
						}

						allWinners.Add(winner);

						var gp = eligibleHands[winner].gamePlayer;
						potPayoutsList.Add(new { playerId = gp.PlayerId.ToString(), playerName = winner, amount = payout });
					}

					// Mark pot as awarded
					var winReason = potWinners.Count > 1
						? $"Split pot - {eligibleHands[potWinners[0]].hand.Type}"
						: eligibleHands[potWinners[0]].hand.Type.ToString();

					pot.IsAwarded = true;
					pot.AwardedAt = now;
					pot.WinReason = winReason;
					pot.WinnerPayouts = System.Text.Json.JsonSerializer.Serialize(potPayoutsList);

					// Track overall win reason (use main pot's reason)
					if (pot.PotOrder == 0)
					{
						overallWinReason = winReason;
					}
				}

				// Update player chip stacks
				foreach (var payout in payouts)
				{
					var gamePlayer = playerHandEvaluations[payout.Key].gamePlayer;
					gamePlayer.ChipStack += payout.Value;
				}

				// 12. Update game state
				game.CurrentPhase = nameof(Phases.Complete);
				game.UpdatedAt = now;
				game.HandCompletedAt = now;
				game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
				UpdateSitOutStatus(game);
				MoveDealer(game);

				await context.SaveChangesAsync(cancellationToken);

				// Record hand history for showdown
				var winnerInfos = allWinners.Select(w =>
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
					winnerNames: allWinners.ToList(),
					winningHandDescription: overallWinReason,
					cancellationToken);
			}
			else
			{
				// Pots already awarded - just build payouts for response
				foreach (var pot in currentHandPots.Where(p => p.WinnerPayouts != null))
				{
					try
					{
						var potPayouts = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(pot.WinnerPayouts!);
						if (potPayouts != null)
						{
							foreach (var potPayout in potPayouts)
							{
								if (potPayout.TryGetValue("playerName", out var nameObj) && 
									potPayout.TryGetValue("amount", out var amountObj))
								{
									var name = nameObj.ToString()!;
									var amount = Convert.ToInt32(((System.Text.Json.JsonElement)amountObj).GetInt32());
									if (payouts.TryGetValue(name, out var existing))
									{
										payouts[name] = existing + amount;
									}
									else
									{
										payouts[name] = amount;
									}
									allWinners.Add(name);
								}
							}
						}
					}
					catch
					{
						// Ignore parsing errors for legacy data
					}
				}
			}

			// 13. Build response
			var playerHandsList = playerHandEvaluations.Select(kvp =>
			{
				var isWinner = allWinners.Contains(kvp.Key);
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
					AmountWon = payouts.GetValueOrDefault(kvp.Key, 0)
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
	/// Updates the status of players with zero chips to sitting out.
	/// </summary>
	private static void UpdateSitOutStatus(Game game)
	{
		foreach (var player in game.GamePlayers)
		{
			if (player.ChipStack <= 0 && player.Status == GamePlayerStatus.Active)
			{
				player.IsSittingOut = true;
				player.Status = GamePlayerStatus.SittingOut;
			}
		}
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
				FoldStreet = gp.HasFolded ? "FirstRound" : null // Simplified for Five Card Draw
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

