using System.Text.Json;
using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.Showdown;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;

/// <summary>
///     Handles the <see cref="PerformShowdownCommand" /> to evaluate Hold'Em hands and award pots.
///     Hold'Em hand evaluation uses 2 hole cards + 5 community cards → best 5-of-7.
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
			.Include(g => g.GameType)
			.Include(g => g.Pots)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
			return new PerformShowdownError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = PerformShowdownErrorCode.GameNotFound
			};

		// Filter pots to current hand
		var currentHandPots = game.Pots.Where(p => p.HandNumber == game.CurrentHandNumber).ToList();
		var isAlreadyAwarded = currentHandPots.Any(p => p.IsAwarded);

		// 2. Validate game is in showdown phase
		if (game.CurrentPhase != "Showdown" && !isAlreadyAwarded)
			return new PerformShowdownError
			{
				Message = $"Cannot perform showdown. Game is in '{game.CurrentPhase}' phase. " +
				          "Showdown can only be performed when the game is in 'Showdown' phase.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};

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
			.ToDictionaryAsync(u => u.Email, u => u.FirstName, StringComparer.OrdinalIgnoreCase, cancellationToken);

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

					var winnerPayoutsList = new[]
					{
						new
						{
							playerId = winner.PlayerId.ToString(), playerName = winner.Player.Name, amount = totalPot
						}
					};
					pot.WinnerPayouts = JsonSerializer.Serialize(winnerPayoutsList);
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
				RecordCompletedHandMetrics(game, currentHandPots, now);

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
					false,
					cancellationToken);
			}

			usersByEmail.TryGetValue(winner.Player.Email ?? string.Empty, out var winnerFirstName);

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
						PlayerFirstName = winnerFirstName,
						Cards = WinByFoldResponseBuilder.HiddenCards<ShowdownCard>(),
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
		var playerHandEvaluations =
			new Dictionary<string, (HoldemHand hand, List<GameCard> allCards, GamePlayer gamePlayer)>();

		foreach (var gamePlayer in playersInHand)
		{
			if (!playerCardGroups.TryGetValue(gamePlayer.Id, out var holeCardEntities)) continue;

			if (holeCardEntities.Count < 2) continue;

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
			return new PerformShowdownError
			{
				Message = "No eligible hands could be evaluated at showdown.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};

		if (isAlreadyAwarded)
			return BuildContestedSuccess(game, playerHandEvaluations, usersByEmail, ReadStoredPayouts(currentHandPots));

		var contributionSum = game.GamePlayers.Sum(gamePlayer => gamePlayer.TotalContributedThisHand);
		if (totalPot != contributionSum)
			return new PerformShowdownError
			{
				Message = $"Pot total {totalPot} does not equal contributed chips {contributionSum}.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};

		var playersInHandIds = playersInHand.Select(gamePlayer => gamePlayer.Id).ToHashSet();
		var stakes = game.GamePlayers.Select(gamePlayer => new HoldEmPotLayers.Stake(
			gamePlayer.Id,
			gamePlayer.Player.Name,
			gamePlayer.TotalContributedThisHand,
			playersInHandIds.Contains(gamePlayer.Id),
			gamePlayer.IsAllIn || gamePlayer.ChipStack == 0)).ToList();

		var layers = HoldEmPotLayers.Build(stakes);
		var layerError = ValidateLayers(layers, stakes.Sum(stake => stake.Contributed));
		if (layerError is not null)
			return layerError;

		if (layers.Uncalled > 0 && layers.UncalledPlayerId is Guid refundedId)
		{
			var refunded = game.GamePlayers.First(gamePlayer => gamePlayer.Id == refundedId);
			refunded.ChipStack += layers.Uncalled;
			refunded.TotalContributedThisHand -= layers.Uncalled;
		}

		var newPots = await ReplaceCurrentHandPotsAsync(game, currentHandPots, layers.Layers, now, cancellationToken);
		var payouts = new Dictionary<string, int>();
		var anyPotSplit = false;
		foreach (var pot in newPots.OrderBy(pot => pot.PotOrder))
		{
			var awardError = AwardPot(
				pot, playersInHand, playerHandEvaluations, payouts, now, game.DealerPosition, ref anyPotSplit);
			if (awardError is not null)
				return awardError;
		}

		foreach (var payout in payouts)
			playerHandEvaluations[payout.Key].gamePlayer.ChipStack += payout.Value;

		await handSettlementService.SettleHandAsync(game, payouts, cancellationToken);

		game.CurrentPhase = "Complete";
		game.UpdatedAt = now;
		game.HandCompletedAt = now;
		game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
		MoveDealer(game);

		await context.SaveChangesAsync(cancellationToken);
		RecordCompletedHandMetrics(game, currentHandPots, now);

		var winnerInfos = payouts
			.Where(payout => payout.Value > 0)
			.Select(payout =>
			{
				var gamePlayer = playerHandEvaluations[payout.Key].gamePlayer;
				return (PlayerId: gamePlayer.PlayerId, PlayerName: payout.Key, AmountWon: payout.Value);
			}).ToList();

		await RecordHandHistoryAsync(
			game,
			game.GamePlayers.ToList(),
			now,
			newPots.Sum(pot => pot.Amount),
			false,
			winnerInfos,
			winnerInfos.Select(winner => winner.PlayerName).ToList(),
			newPots.FirstOrDefault(pot => pot.PotOrder == 0)?.WinReason,
			playerHandEvaluations,
			anyPotSplit,
			cancellationToken);

		return BuildContestedSuccess(game, playerHandEvaluations, usersByEmail, payouts);
	}

	private static Dictionary<string, int> ReadStoredPayouts(IEnumerable<Pot> currentHandPots)
	{
		var payouts = new Dictionary<string, int>();
		foreach (var pot in currentHandPots.Where(pot => pot.WinnerPayouts != null))
			try
			{
				var potPayouts = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(pot.WinnerPayouts!);
				if (potPayouts is null)
					continue;

				foreach (var potPayout in potPayouts)
					if (potPayout.TryGetValue("playerName", out var nameObj) &&
					    potPayout.TryGetValue("amount", out var amountObj))
					{
						var name = nameObj.ToString()!;
						var amount = Convert.ToInt32(((JsonElement)amountObj).GetInt32());
						if (payouts.TryGetValue(name, out var existing))
							payouts[name] = existing + amount;
						else
							payouts[name] = amount;
					}
			}
			catch
			{
				// Ignore parsing errors for legacy data
			}

		return payouts;
	}

	private static PerformShowdownError? ValidateLayers(HoldEmPotLayers.BuildResult layers, int contributedSum)
	{
		var layered = layers.Uncalled + layers.Layers.Sum(layer => layer.Amount);
		if (layered != contributedSum)
			return new PerformShowdownError
			{
				Message =
					$"Uncalled chips plus pot layers ({layered}) do not equal contributed chips ({contributedSum}).",
				Code = PerformShowdownErrorCode.InvalidGameState
			};

		foreach (var layer in layers.Layers)
		{
			var sliceSum = layer.Slices.Sum(slice => slice.Amount);
			if (sliceSum != layer.Amount)
				return new PerformShowdownError
				{
					Message = $"Layer {layer.PotOrder} slices total {sliceSum} but the layer amount is {layer.Amount}.",
					Code = PerformShowdownErrorCode.InvalidGameState
				};

			if (layer.Amount <= 0 || !layer.Slices.Any(slice => slice.Eligible))
				return new PerformShowdownError
				{
					Message = $"Layer {layer.PotOrder} amount {layer.Amount} has no eligible winner.",
					Code = PerformShowdownErrorCode.InvalidGameState
				};
		}

		return null;
	}

	private async Task<List<Pot>> ReplaceCurrentHandPotsAsync(
		Game game,
		List<Pot> currentHandPots,
		IReadOnlyList<HoldEmPotLayers.Layer> layers,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var potIds = currentHandPots.Select(pot => pot.Id).ToList();
		var existingContributions = await context.PotContributions
			.Where(contribution => potIds.Contains(contribution.PotId))
			.ToListAsync(cancellationToken);
		context.PotContributions.RemoveRange(existingContributions);

		foreach (var pot in currentHandPots)
		{
			context.Pots.Remove(pot);
			game.Pots.Remove(pot);
		}

		var newPots = new List<Pot>();
		foreach (var layer in layers)
		{
			var pot = new Pot
			{
				GameId = game.Id,
				Game = game,
				HandNumber = game.CurrentHandNumber,
				PotOrder = layer.PotOrder,
				PotType = layer.PotOrder == 0 ? PotType.Main : PotType.Side,
				Amount = layer.Amount,
				MaxContributionPerPlayer = layer.MaxContributionPerPlayer,
				CreatedAt = now,
				IsAwarded = false
			};

			foreach (var slice in layer.Slices.Where(slice => slice.Amount > 0 || slice.Eligible))
			{
				var gamePlayer = game.GamePlayers.First(player => player.Id == slice.GamePlayerId);
				pot.Contributions.Add(new PotContribution
				{
					Pot = pot,
					GamePlayerId = slice.GamePlayerId,
					GamePlayer = gamePlayer,
					Amount = slice.Amount,
					IsEligibleToWin = slice.Eligible,
					ContributedAt = now
				});
			}

			context.Pots.Add(pot);
			newPots.Add(pot);
		}

		return newPots;
	}

	private static PerformShowdownError? AwardPot(
		Pot pot,
		List<GamePlayer> playersInHand,
		Dictionary<string, (HoldemHand hand, List<GameCard> allCards, GamePlayer gamePlayer)> playerHandEvaluations,
		Dictionary<string, int> payouts,
		DateTimeOffset now,
		int dealerPosition,
		ref bool anyPotSplit)
	{
		var eligibleIds = pot.Contributions
			.Where(contribution => contribution.IsEligibleToWin)
			.Select(contribution => contribution.GamePlayerId)
			.ToHashSet();
		var evaluatedIds = playerHandEvaluations.Values.Select(evaluation => evaluation.gamePlayer.Id).ToHashSet();
		if (eligibleIds.Count > 0 && !eligibleIds.Any(evaluatedIds.Contains))
			return new PerformShowdownError
			{
				Message =
					$"Pot {pot.PotOrder} has eligible contributions but none of those players were evaluated.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};

		var contenders = new List<(string Name, HoldemHand Hand, GamePlayer Player)>();
		foreach (var gamePlayer in playersInHand)
		{
			if (!eligibleIds.Contains(gamePlayer.Id))
				continue;
			if (!playerHandEvaluations.TryGetValue(gamePlayer.Player.Name, out var evaluation))
				continue;

			contenders.Add((gamePlayer.Player.Name, evaluation.hand, evaluation.gamePlayer));
		}

		if (contenders.Count == 0)
			return new PerformShowdownError
			{
				Message = $"Pot {pot.PotOrder} has no evaluated player eligible to win it.",
				Code = PerformShowdownErrorCode.InvalidGameState
			};

		var maxStrength = contenders.Max(contender => contender.Hand.Strength);
		var winners = contenders.Where(contender => contender.Hand.Strength == maxStrength).ToList();
		var orderedWinners = winners
			.OrderBy(winner => winner.Player.SeatPosition > dealerPosition ? 0 : 1)
			.ThenBy(winner => winner.Player.SeatPosition)
			.ToList();
		var share = pot.Amount / orderedWinners.Count;
		var remainder = pot.Amount % orderedWinners.Count;
		var potWinnerPayouts = new List<(GamePlayer Player, string Name, int Amount)>();
		foreach (var winner in orderedWinners)
		{
			var amount = share;
			if (remainder > 0)
			{
				amount++;
				remainder--;
			}

			if (amount <= 0)
				continue;

			potWinnerPayouts.Add((winner.Player, winner.Name, amount));
			payouts[winner.Name] = payouts.GetValueOrDefault(winner.Name) + amount;
		}

		if (winners.Count > 1)
			anyPotSplit = true;

		var winningHand = winners[0].Hand;
		pot.IsAwarded = true;
		pot.AwardedAt = now;
		pot.WinReason = winners.Count > 1
			? $"Split pot - {winningHand.Type}"
			: winningHand.Type.ToString();
		pot.WinnerPayouts = JsonSerializer.Serialize(potWinnerPayouts.Select(payout => new
		{
			playerId = payout.Player.PlayerId.ToString(),
			playerName = payout.Name,
			amount = payout.Amount
		}));
		return null;
	}

	private static PerformShowdownSuccessful BuildContestedSuccess(
		Game game,
		Dictionary<string, (HoldemHand hand, List<GameCard> allCards, GamePlayer gamePlayer)> playerHandEvaluations,
		Dictionary<string, string?> usersByEmail,
		Dictionary<string, int> payouts)
	{
		var playerHandsList = playerHandEvaluations.Select(kvp =>
		{
			var amountWon = payouts.GetValueOrDefault(kvp.Key, 0);
			usersByEmail.TryGetValue(kvp.Value.gamePlayer.Player.Email ?? string.Empty, out var firstName);

			var bestHand = FindBestFiveCardHand(kvp.Value.hand);
			var cardsList = kvp.Value.allCards;
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
				PlayerFirstName = firstName,
				Cards = kvp.Value.allCards.Select(c => new ShowdownCard
				{
					Suit = c.Suit,
					Symbol = c.Symbol
				}).ToList(),
				HandType = kvp.Value.hand.Type.ToString(),
				HandStrength = kvp.Value.hand.Strength,
				IsWinner = amountWon > 0,
				AmountWon = amountWon,
				BestCardIndexes = bestCardIndexes
			};
		}).OrderByDescending(hand => hand.HandStrength ?? 0).ToList();

		return new PerformShowdownSuccessful
		{
			GameId = game.Id,
			WonByFold = false,
			CurrentPhase = game.CurrentPhase,
			Payouts = payouts,
			PlayerHands = playerHandsList
		};
	}

	private void RecordCompletedHandMetrics(Game game, IEnumerable<Pot> currentHandPots, DateTimeOffset completedAt)
	{
		telemetry?.RecordHandCompleted(GameEventsTelemetry.ResolveGameType(game),
			GameEventsTelemetry.ResolveTableKind(game));

		var handDurationMs = GameEventsTelemetry.TryGetHandDurationMs(currentHandPots, completedAt);
		if (handDurationMs.HasValue)
		{
			telemetry?.RecordHandDuration(GameEventsTelemetry.ResolveGameType(game),
				GameEventsTelemetry.ResolveTableKind(game), handDurationMs.Value);
		}
	}

	/// <summary>
	///     Moves the dealer button to the next occupied seat position (clockwise).
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
		var seatsAfterCurrent = occupiedSeats.Where(pos => pos > currentPosition).ToList();

		game.DealerPosition = seatsAfterCurrent.Count > 0 ? seatsAfterCurrent.First() : occupiedSeats.First();
	}

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
		Dictionary<string, (HoldemHand hand, List<GameCard> allCards, GamePlayer gamePlayer)>? playerHandEvaluations,
		bool isSplitPot,
		CancellationToken cancellationToken)
	{
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
	///     Finds the best 5-card hand from a HoldemHand by evaluating all C(7,5) combinations.
	/// </summary>
	private static List<Card> FindBestFiveCardHand(HoldemHand hand)
	{
		var allCards = hand.HoleCards.Concat(hand.CommunityCards).ToList();

		if (allCards.Count <= 5) return allCards;

		var ranking = HandTypeStrengthRanking.Classic;
		List<Card>? bestCombo = null;
		var bestStrength = long.MinValue;

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