using System.Text.Json;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.ScrewYourNeighbor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Screw Your Neighbor.
/// </summary>
/// <remarks>
/// <para>
/// Screw Your Neighbor is a multi-hand elimination card game (not traditional poker):
/// </para>
/// <list type="number">
///   <item><description>Each player starts with three stacks of chips (buy-in).</description></item>
///   <item><description>Deal one card face-down to each active player.</description></item>
///   <item><description>Starting left of dealer, each player decides: keep their card or trade with the player to their left.</description></item>
///   <item><description>Kings are blockers — a player with a King cannot trade and cannot be traded with.</description></item>
///   <item><description>The dealer can trade with the top card of the deck.</description></item>
///   <item><description>After all decisions, cards are revealed. Player(s) with the lowest card lose one stack to the center pot.</description></item>
///   <item><description>If the tie would eliminate all remaining players, no one pays and a new round is dealt.</description></item>
///   <item><description>Play continues until one player remains with stacks — that player wins the pot.</description></item>
/// </list>
/// <para>
/// Aces are always low. Kings are the highest card and act as blockers.
/// </para>
/// </remarks>
public sealed class ScrewYourNeighborFlowHandler : BaseGameFlowHandler
{
	/// <inheritdoc />
	public override string GameTypeCode => "SCREWYOURNEIGHBOR";

	/// <inheritdoc />
	public override GameRules GetGameRules() => ScrewYourNeighborRules.CreateGameRules();

	/// <inheritdoc />
	public override string GetInitialPhase(Game game) => nameof(Phases.Dealing);

	/// <inheritdoc />
	public override string? GetNextPhase(Game game, string currentPhase)
	{
		return currentPhase switch
		{
			nameof(Phases.Dealing) => nameof(Phases.KeepOrTrade),
			nameof(Phases.KeepOrTrade) => nameof(Phases.Reveal),
			nameof(Phases.Reveal) => nameof(Phases.Showdown),
			nameof(Phases.Showdown) => nameof(Phases.Complete),
			_ => base.GetNextPhase(game, currentPhase)
		};
	}

	/// <inheritdoc />
	public override DealingConfiguration GetDealingConfiguration()
	{
		return new DealingConfiguration
		{
			PatternType = DealingPatternType.AllAtOnce,
			InitialCardsPerPlayer = 1,
			AllFaceDown = true
		};
	}

	/// <inheritdoc />
	public override bool SkipsAnteCollection => true;

	/// <inheritdoc />
	public override IReadOnlyList<string> SpecialPhases =>
		[nameof(Phases.KeepOrTrade), nameof(Phases.Reveal)];

	/// <inheritdoc />
	public override bool IsMultiHandVariant => true;

	/// <inheritdoc />
	public override bool RequiresChipCoverageCheck => false;

	/// <inheritdoc />
	public override ChipCheckConfiguration GetChipCheckConfiguration() =>
		ChipCheckConfiguration.Disabled;

	/// <inheritdoc />
	public override bool SupportsInlineShowdown => true;

	/// <inheritdoc />
	public override Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default)
	{
		// Reset player state for new round
		foreach (var player in game.GamePlayers)
		{
			player.HasFolded = false;
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public override async Task PrepareForNewHandAsync(
		CardsDbContext context,
		Game game,
		List<GamePlayer> eligiblePlayers,
		int upcomingHandNumber,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var existingCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id)
			.ToListAsync(cancellationToken);

		if (existingCards.Count == 0)
		{
			return;
		}

		var remainingDeckCards = existingCards
			.Where(gc => gc.Location == CardLocation.Deck && gc.GamePlayerId == null && !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.ToList();

		var requiredCardsForNextHand = eligiblePlayers.Count + 1;
		if (remainingDeckCards.Count < requiredCardsForNextHand)
		{
			context.GameCards.RemoveRange(existingCards);
			return;
		}

		var remainingDeckCardIds = remainingDeckCards
			.Select(gc => gc.Id)
			.ToHashSet();

		var cardsToRemove = existingCards
			.Where(gc => !remainingDeckCardIds.Contains(gc.Id))
			.ToList();

		if (cardsToRemove.Count > 0)
		{
			context.GameCards.RemoveRange(cardsToRemove);
		}

		foreach (var deckCard in remainingDeckCards)
		{
			deckCard.HandNumber = upcomingHandNumber;
			deckCard.DealtAt = now;
		}
	}

	/// <inheritdoc />
	public override async Task DealCardsAsync(
		CardsDbContext context,
		Game game,
		List<GamePlayer> eligiblePlayers,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var config = GetDealingConfiguration();
		var cardsPerPlayer = config.InitialCardsPerPlayer > 0 ? config.InitialCardsPerPlayer : 1;
		var requiredCardsForHand = eligiblePlayers.Count + 1;

		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             gc.Location == CardLocation.Deck &&
			             gc.GamePlayerId == null &&
			             !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		if (deckCards.Count < requiredCardsForHand)
		{
			if (deckCards.Count > 0)
			{
				context.GameCards.RemoveRange(deckCards);
			}

			deckCards = await CreateFreshDeckAsync(context, game, now, cancellationToken);
		}

		var dealerPosition = game.DealerPosition;
		var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
		var totalSeats = maxSeatPosition + 1;

		var playersInDealOrder = eligiblePlayers
			.OrderBy(p => (p.SeatPosition - dealerPosition - 1 + totalSeats) % totalSeats)
			.ToList();

		var deckIndex = 0;
		var dealOrder = 1;
		foreach (var player in playersInDealOrder)
		{
			for (var cardIndex = 0; cardIndex < cardsPerPlayer; cardIndex++)
			{
				if (deckIndex >= deckCards.Count)
				{
					break;
				}

				var card = deckCards[deckIndex++];
				card.GamePlayerId = player.Id;
				card.Location = CardLocation.Hand;
				card.DealOrder = dealOrder++;
				card.IsVisible = !config.AllFaceDown || IsKing(card.Symbol);
				card.DealtAt = now;
			}
		}

		game.CurrentPhase = nameof(Phases.KeepOrTrade);
		game.CurrentPlayerIndex = FindFirstActivePlayerAfterDealer(game, eligiblePlayers);
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);
	}

	private static async Task<List<GameCard>> CreateFreshDeckAsync(
		CardsDbContext context,
		Game game,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var shuffledDeck = CreateShuffledDeck();
		var deckCards = new List<GameCard>(shuffledDeck.Count);

		for (var deckOrder = 0; deckOrder < shuffledDeck.Count; deckOrder++)
		{
			var (suit, symbol) = shuffledDeck[deckOrder];
			var gameCard = new GameCard
			{
				GameId = game.Id,
				GamePlayerId = null,
				HandNumber = game.CurrentHandNumber,
				Suit = suit,
				Symbol = symbol,
				DealOrder = deckOrder,
				Location = CardLocation.Deck,
				IsVisible = false,
				IsDiscarded = false,
				DealtAt = now
			};

			deckCards.Add(gameCard);
		}

		context.GameCards.AddRange(deckCards);
		await context.SaveChangesAsync(cancellationToken);

		return deckCards;
	}

	#region Showdown

	/// <inheritdoc />
	public override async Task<ShowdownResult> PerformShowdownAsync(
		CardsDbContext context,
		Game game,
		IHandHistoryRecorder handHistoryRecorder,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.IsSittingOut && !gp.HasFolded)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		// Use the in-memory navigation property instead of a DB query so that
		// unsaved card changes (e.g. dealer deck trade within the same command)
		// are correctly reflected in the showdown evaluation.
		var handCards = game.GameCards
			.Where(gc => gc.HandNumber == game.CurrentHandNumber &&
			             gc.GamePlayerId != null &&
			             gc.Location == CardLocation.Hand &&
			             !gc.IsDiscarded)
			.ToList();

		// Build player-card map
		var playerCards = new Dictionary<Guid, GameCard>();
		foreach (var card in handCards)
		{
			if (card.GamePlayerId.HasValue)
			{
				playerCards[card.GamePlayerId.Value] = card;
			}
		}

		// Reveal all cards
		foreach (var card in handCards)
		{
			card.IsVisible = true;
		}

		// SYN stack loss is based on configured ante/stack size, but zero/negative
		// ante values should still use the default stack size.
		var stackSize = game.Ante.GetValueOrDefault() > 0 ? game.Ante!.Value : 25;

		// Find lowest card value (Ace is low = 1, King is high = 13)
		var playerValues = new List<(GamePlayer Player, int CardValue, GameCard Card)>();
		foreach (var player in activePlayers)
		{
			if (playerCards.TryGetValue(player.Id, out var card))
			{
				var value = GetScrewYourNeighborCardValue(card.Symbol);
				playerValues.Add((player, value, card));
			}
		}

		if (playerValues.Count == 0)
		{
			return ShowdownResult.Failure("No cards to evaluate");
		}

		var lowestValue = playerValues.Min(pv => pv.CardValue);
		var losers = playerValues.Where(pv => pv.CardValue == lowestValue).Select(pv => pv.Player).ToList();
		var winners = activePlayers.Where(p => !losers.Contains(p)).ToList();

		// Check safety rule: if all remaining players would be eliminated, nobody pays
		var allWouldBeEliminated = losers.All(l => l.ChipStack <= stackSize)
		                           && losers.Count == activePlayers.Count;

		// Load main pot
		var mainPot = await context.Pots
			.FirstOrDefaultAsync(p => p.GameId == game.Id &&
			                          p.HandNumber == game.CurrentHandNumber &&
			                          p.PotType == PotType.Main,
				cancellationToken);

		if (mainPot == null)
		{
			return ShowdownResult.Failure("No pot found");
		}

		var totalLost = 0;

		if (!allWouldBeEliminated)
		{
			// Losers lose one stack each
			foreach (var loser in losers)
			{
				var lossAmount = Math.Min(stackSize, loser.ChipStack);
				loser.ChipStack -= lossAmount;
				loser.TotalContributedThisHand += lossAmount;
				totalLost += lossAmount;
			}

			// Add lost stacks to pot
			mainPot.Amount += totalLost;
		}

		// Check if game is over (only one player with chips remaining)
		var playersWithChips = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && gp.ChipStack > 0)
			.ToList();

		if (playersWithChips.Count <= 1 && playersWithChips.Count > 0)
		{
			// Game over — award entire pot to last player standing
			var gameWinner = playersWithChips[0];
			gameWinner.ChipStack += mainPot.Amount;
			mainPot.IsAwarded = true;
			mainPot.AwardedAt = now;
			mainPot.WinnerPayouts = JsonSerializer.Serialize(new[]
			{
				new
				{
					playerId = gameWinner.PlayerId.ToString(),
					playerName = gameWinner.Player?.Name ?? "Unknown",
					amount = mainPot.Amount
				}
			});

			game.HandCompletedAt = now;
			game.UpdatedAt = now;

			if (game.IsDealersChoice)
			{
				// On a Dealer's Choice table, terminal SYN ends only the chosen variant.
				// The background service will hand control back to WaitingForDealerChoice.
				game.NextHandStartsAt = now.AddSeconds(4);
				game.Status = GameStatus.InProgress;
			}
			else
			{
				game.NextHandStartsAt = null;
				game.Status = GameStatus.Completed;
			}

			await context.SaveChangesAsync(cancellationToken);

			// Record hand history
			await RecordHandHistoryAsync(
				handHistoryRecorder, game, activePlayers,
				mainPot.Amount, [gameWinner], losers,
				$"{gameWinner.Player?.Name ?? "Unknown"} wins the game!", now, cancellationToken);

			return ShowdownResult.Success(
				[gameWinner.PlayerId],
				losers.Select(l => l.PlayerId).ToList(),
				mainPot.Amount,
				$"{gameWinner.Player?.Name ?? "Unknown"} wins the game!");
		}

		// Game continues: carry pot to next hand
		var nextHandPot = new Data.Entities.Pot
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber + 1,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = mainPot.Amount,
			IsAwarded = false,
			CreatedAt = now
		};
		context.Pots.Add(nextHandPot);

		// Mark current pot as awarded (value transferred to next hand)
		mainPot.IsAwarded = true;
		mainPot.AwardedAt = now;

		// Build description of what happened
		var loserNames = string.Join(", ", losers.Select(l => l.Player?.Name ?? "Unknown"));
		var lowestCardName = GetCardDisplayName(playerValues.First(pv => pv.CardValue == lowestValue).Card);
		var description = allWouldBeEliminated
			? $"All remaining players tied with {lowestCardName} — nobody loses a stack"
			: $"{loserNames} had the lowest card ({lowestCardName}) and lost a stack";

		// Set timestamps for continuous play
		game.HandCompletedAt = now;
		var secondsBetweenScrewYourNeighborHandDeals = 4;
		game.NextHandStartsAt = now.AddSeconds(secondsBetweenScrewYourNeighborHandDeals);
		game.UpdatedAt = now;
		MoveDealer(game);

		// Sit out eliminated players
		foreach (var player in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active && gp.ChipStack <= 0))
		{
			player.IsSittingOut = true;
		}

		await context.SaveChangesAsync(cancellationToken);

		// Record hand history
		await RecordHandHistoryAsync(
			handHistoryRecorder, game, activePlayers,
			totalLost, winners, losers, description, now, cancellationToken);

		return ShowdownResult.Success(
			winners.Select(w => w.PlayerId).ToList(),
			losers.Select(l => l.PlayerId).ToList(),
			totalLost,
			description);
	}

	/// <inheritdoc />
	public override Task<string> ProcessPostShowdownAsync(
		CardsDbContext context,
		Game game,
		ShowdownResult showdownResult,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (game.Status == GameStatus.Completed)
		{
			return Task.FromResult("Ended");
		}

		return Task.FromResult(nameof(Phases.Complete));
	}

	#endregion

	#region Auto Actions

	/// <inheritdoc />
	public override async Task PerformAutoActionAsync(AutoActionContext context)
	{
		if (context.CurrentPhase.Equals(nameof(Phases.KeepOrTrade), StringComparison.OrdinalIgnoreCase))
		{
			// Auto-action for timed-out player: keep their card
			await PerformAutoKeepAsync(context);
		}
	}

	private async Task PerformAutoKeepAsync(AutoActionContext context)
	{
		var player = context.Game.GamePlayers.FirstOrDefault(gp => gp.SeatPosition == context.PlayerSeatIndex);
		if (player is null)
		{
			player = await context.DbContext.GamePlayers
				.AsNoTracking()
				.FirstOrDefaultAsync(gp => gp.GameId == context.GameId && gp.SeatPosition == context.PlayerSeatIndex,
					context.CancellationToken);
		}

		if (player is null) return;

		try
		{
			var command = new KeepOrTradeCommand(context.GameId, player.PlayerId, "Keep");
			await context.Mediator.Send(command, context.CancellationToken);
			context.Logger.LogInformation(
				"Auto-keep completed for player {PlayerId} in SYN game {GameId}",
				player.PlayerId, context.GameId);
		}
		catch (Exception ex)
		{
			context.Logger.LogError(ex,
				"Auto-keep failed for player {PlayerId} in SYN game {GameId}",
				player.PlayerId, context.GameId);
		}
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Gets the SYN card value where Ace is low (1) and King is high (13).
	/// </summary>
	internal static int GetScrewYourNeighborCardValue(CardSymbol symbol)
	{
		return symbol == CardSymbol.Ace ? 1 : (int)symbol;
	}

	/// <summary>
	/// Checks if a card symbol is a King (blocker).
	/// </summary>
	internal static bool IsKing(CardSymbol symbol) => symbol == CardSymbol.King;

	private static string GetCardDisplayName(GameCard card)
	{
		var symbolName = card.Symbol switch
		{
			CardSymbol.Ace => "Ace",
			CardSymbol.Deuce => "2",
			CardSymbol.Three => "3",
			CardSymbol.Four => "4",
			CardSymbol.Five => "5",
			CardSymbol.Six => "6",
			CardSymbol.Seven => "7",
			CardSymbol.Eight => "8",
			CardSymbol.Nine => "9",
			CardSymbol.Ten => "10",
			CardSymbol.Jack => "Jack",
			CardSymbol.Queen => "Queen",
			CardSymbol.King => "King",
			_ => "?"
		};

		var suitName = card.Suit switch
		{
			CardSuit.Hearts => "Hearts",
			CardSuit.Diamonds => "Diamonds",
			CardSuit.Spades => "Spades",
			CardSuit.Clubs => "Clubs",
			_ => "?"
		};

		return $"{symbolName} of {suitName}";
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
			CardSymbol.Ten => "10",
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

	/// <summary>
	/// Moves the dealer button to the next active player clockwise.
	/// </summary>
	private static void MoveDealer(Game game)
	{
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.IsSittingOut && gp.ChipStack > 0)
			.OrderBy(gp => gp.SeatPosition)
			.Select(gp => gp.SeatPosition)
			.ToList();

		if (activePlayers.Count == 0)
		{
			activePlayers = game.GamePlayers
				.Where(gp => gp.Status == GamePlayerStatus.Active)
				.OrderBy(gp => gp.SeatPosition)
				.Select(gp => gp.SeatPosition)
				.ToList();
		}

		if (activePlayers.Count == 0) return;

		var currentPosition = game.DealerPosition;
		var seatsAfterCurrent = activePlayers.Where(pos => pos > currentPosition).ToList();

		game.DealerPosition = seatsAfterCurrent.Count > 0
			? seatsAfterCurrent.First()
			: activePlayers.First();
	}

	private static async Task RecordHandHistoryAsync(
		IHandHistoryRecorder handHistoryRecorder,
		Game game,
		List<GamePlayer> activePlayers,
		int potAmount,
		List<GamePlayer> winners,
		List<GamePlayer> losers,
		string? description,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var winnerPlayerIds = winners.Select(w => w.PlayerId).ToHashSet();

		var winnerInfos = winners.Select(w => new WinnerInfo
		{
			PlayerId = w.PlayerId,
			PlayerName = w.Player?.Name ?? w.PlayerId.ToString(),
			AmountWon = 0 // In SYN, winners don't win per-hand — pot accumulates
		}).ToList();

		var playerResults = activePlayers.Select(gp =>
		{
			var isWinner = winnerPlayerIds.Contains(gp.PlayerId);
			var isLoser = losers.Any(l => l.PlayerId == gp.PlayerId);

			var cards = game.GameCards
				.Where(gc => gc.GamePlayerId == gp.Id &&
				             gc.HandNumber == game.CurrentHandNumber &&
				             !gc.IsDiscarded)
				.OrderBy(gc => gc.DealOrder)
				.Select(gc => FormatCard(gc.Symbol, gc.Suit))
				.ToList();

			return new PlayerResultInfo
			{
				PlayerId = gp.PlayerId,
				PlayerName = gp.Player?.Name ?? gp.PlayerId.ToString(),
				SeatPosition = gp.SeatPosition,
				HasFolded = false,
				ReachedShowdown = true,
				IsWinner = isWinner,
				IsSplitPot = false,
				NetChipDelta = isLoser ? -gp.TotalContributedThisHand : 0,
				WentAllIn = false,
				ShowdownCards = cards.Count > 0 ? cards : null
			};
		}).ToList();

		await handHistoryRecorder.RecordHandHistoryAsync(new RecordHandHistoryParameters
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			CompletedAtUtc = now,
			WonByFold = false,
			TotalPot = potAmount,
			WinningHandDescription = description,
			Winners = winnerInfos,
			PlayerResults = playerResults
		}, cancellationToken);
	}

	#endregion
}
