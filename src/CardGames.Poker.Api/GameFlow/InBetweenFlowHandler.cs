using System.Text.Json;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.InBetween;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.InBetween;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static CardGames.Poker.Api.Features.Games.InBetween.InBetweenVariantState;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for In-Between (Acey-Deucey / Between the Sheets).
/// </summary>
/// <remarks>
/// <para>
/// In-Between is a pot-funded card game (not traditional poker):
/// </para>
/// <list type="number">
///   <item><description>All players ante to build the pot.</description></item>
///   <item><description>Starting left of dealer, two boundary cards are dealt face-up.</description></item>
///   <item><description>If the first boundary card is an Ace, the player must declare it high or low.</description></item>
///   <item><description>The player may bet (up to the pot) or pass.</description></item>
///   <item><description>A third card is dealt. If its rank falls strictly between the two boundaries, the player wins their bet from the pot.</description></item>
///   <item><description>If the third card matches a boundary, the player POSTs (pays double their bet into the pot).</description></item>
///   <item><description>If the third card is outside the boundaries, the player loses their bet to the pot.</description></item>
///   <item><description>During the first orbit (before all players have had a turn), full-pot bets are disallowed.</description></item>
///   <item><description>The game ends when the pot is emptied by a winning bet.</description></item>
/// </list>
/// <para>
/// Uses a continuous deck that is reshuffled when 3 or fewer cards remain.
/// </para>
/// </remarks>
public sealed class InBetweenFlowHandler : BaseGameFlowHandler
{
	private const int DeckRefreshThreshold = 3;

	/// <inheritdoc />
	public override string GameTypeCode => "INBETWEEN";

	/// <inheritdoc />
	public override GameRules GetGameRules() => InBetweenRules.CreateGameRules();

	/// <inheritdoc />
	public override string GetInitialPhase(Game game) => nameof(Phases.CollectingAntes);

	/// <inheritdoc />
	public override string? GetNextPhase(Game game, string currentPhase)
	{
		return currentPhase switch
		{
			nameof(Phases.CollectingAntes) => nameof(Phases.InBetweenTurn),
			nameof(Phases.InBetweenTurn) => nameof(Phases.Complete),
			_ => base.GetNextPhase(game, currentPhase)
		};
	}

	/// <inheritdoc />
	public override DealingConfiguration GetDealingConfiguration()
	{
		return new DealingConfiguration
		{
			PatternType = DealingPatternType.AllAtOnce,
			InitialCardsPerPlayer = 0,
			AllFaceDown = false
		};
	}

	/// <inheritdoc />
	public override bool SkipsAnteCollection => false;

	/// <inheritdoc />
	public override bool AutoCollectsAntesOnStart => true;

	/// <inheritdoc />
	public override IReadOnlyList<string> SpecialPhases =>
		[nameof(Phases.InBetweenTurn)];

	/// <inheritdoc />
	public override bool IsMultiHandVariant => false;

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
		// Initialize variant state for the game
		var state = new InBetweenState
		{
			SubPhase = TurnSubPhase.AwaitingFirstBoundary,
			PlayersCompletedFirstTurn = []
		};
		SetState(game, state);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public override async Task DealCardsAsync(
		CardsDbContext context,
		Game game,
		List<GamePlayer> eligiblePlayers,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// Create the initial shuffled deck — no cards dealt to players at start
		var existingCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id && gc.HandNumber == game.CurrentHandNumber)
			.ToListAsync(cancellationToken);

		if (existingCards.Count == 0)
		{
			await CreateFreshDeckAsync(context, game, now, cancellationToken);
		}

		// Set the first player (left of dealer)
		game.CurrentPhase = nameof(Phases.InBetweenTurn);
		game.CurrentPlayerIndex = FindFirstActivePlayerAfterDealer(game, eligiblePlayers);
		game.UpdatedAt = now;

		// Initialize turn state for first player
		UpdateState(game, state =>
		{
			state.SubPhase = TurnSubPhase.AwaitingFirstBoundary;
			state.AceIsHigh = null;
			state.BetAmount = 0;
			state.LastTurnResult = TurnResult.None;
			state.DeckRefreshedThisTurn = false;
			state.LastTurnDescription = null;
		});

		await context.SaveChangesAsync(cancellationToken);
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
		// In-Between is single-hand, so this is a no-op — deck persists across turns
		await Task.CompletedTask;
	}

	#region Turn Management

	/// <summary>
	/// Deals the two boundary cards for the active player's turn.
	/// Handles deck refresh if needed. If first card is an Ace, sets sub-phase to AwaitingAceChoice.
	/// </summary>
	internal static async Task DealBoundaryCardsAsync(
		CardsDbContext context,
		Game game,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var state = GetState(game);
		state.DeckRefreshedThisTurn = false;

		// Check deck and refresh if needed
		var deckCards = await GetAvailableDeckCardsAsync(context, game, cancellationToken);

		if (deckCards.Count <= DeckRefreshThreshold)
		{
			await RefreshDeckAsync(context, game, now, cancellationToken);
			deckCards = await GetAvailableDeckCardsAsync(context, game, cancellationToken);
			state.DeckRefreshedThisTurn = true;
		}

		// Need at least 3 cards for a complete turn
		if (deckCards.Count < 3)
		{
			await RefreshDeckAsync(context, game, now, cancellationToken);
			deckCards = await GetAvailableDeckCardsAsync(context, game, cancellationToken);
			state.DeckRefreshedThisTurn = true;
		}

		// Deal first boundary card
		var firstCard = deckCards[0];
		firstCard.Location = CardLocation.Community;
		firstCard.DealOrder = 1;
		firstCard.IsVisible = true;
		firstCard.DealtAt = now;

		// Deal second boundary card
		var secondCard = deckCards[1];
		secondCard.Location = CardLocation.Community;
		secondCard.DealOrder = 2;
		secondCard.IsVisible = true;
		secondCard.DealtAt = now;

		// Check if first card is an Ace — requires player to choose high or low
		if (firstCard.Symbol == CardSymbol.Ace)
		{
			state.SubPhase = TurnSubPhase.AwaitingAceChoice;
		}
		else
		{
			state.SubPhase = TurnSubPhase.AwaitingBetOrPass;
		}

		state.AceIsHigh = null;
		state.BetAmount = 0;
		state.LastTurnResult = TurnResult.None;
		state.LastTurnDescription = null;

		SetState(game, state);
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);
	}

	/// <summary>
	/// Resolves the current turn: deals the third card, compares it to boundaries, and adjusts chips/pot.
	/// </summary>
	internal static async Task<TurnResult> ResolveTurnAsync(
		CardsDbContext context,
		Game game,
		int betAmount,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var state = GetState(game);

		// Get the turn's boundary cards (community cards for this hand, dealt this turn)
		var turnCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             gc.Location == CardLocation.Community &&
			             !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		// Get the two most recently dealt boundary cards
		var boundaryCards = turnCards.TakeLast(2).ToList();
		if (boundaryCards.Count < 2)
			throw new InvalidOperationException("Expected two boundary cards for resolution.");

		// Deal third card
		var deckCards = await GetAvailableDeckCardsAsync(context, game, cancellationToken);
		if (deckCards.Count == 0)
		{
			await RefreshDeckAsync(context, game, now, cancellationToken);
			deckCards = await GetAvailableDeckCardsAsync(context, game, cancellationToken);
		}

		var thirdCard = deckCards[0];
		thirdCard.Location = CardLocation.Community;
		thirdCard.DealOrder = 3;
		thirdCard.IsVisible = true;
		thirdCard.DealtAt = now;

		// Resolve boundaries
		var low = GetEffectiveValue(boundaryCards[0].Symbol, false); // first boundary
		var high = GetEffectiveValue(boundaryCards[1].Symbol, true); // second boundary

		// If ace choice was made, apply it to the first card
		if (boundaryCards[0].Symbol == CardSymbol.Ace && state.AceIsHigh.HasValue)
		{
			low = state.AceIsHigh.Value ? 14 : 1;
		}

		// Ensure low < high
		if (low > high)
			(low, high) = (high, low);

		var thirdValue = GetEffectiveValue(thirdCard.Symbol, null);

		// Load pot and active player
		var mainPot = await context.Pots
			.FirstOrDefaultAsync(p => p.GameId == game.Id &&
			                          p.HandNumber == game.CurrentHandNumber &&
			                          p.PotType == PotType.Main,
				cancellationToken) ?? throw new InvalidOperationException("No main pot found.");

		var activePlayer = game.GamePlayers
			.FirstOrDefault(gp => gp.SeatPosition == game.CurrentPlayerIndex)
			?? throw new InvalidOperationException("Active player not found.");

		TurnResult result;
		string description;

		if (thirdValue == low || thirdValue == high)
		{
			// POST: matches a boundary — player pays double bet to pot
			var postAmount = betAmount * 2;
			var actualPost = Math.Min(postAmount, activePlayer.ChipStack);
			activePlayer.ChipStack -= actualPost;
			mainPot.Amount += actualPost;
			result = TurnResult.Post;
			description = $"{activePlayer.Player?.Name ?? "Unknown"} POSTs {actualPost} (matched boundary)";
		}
		else if (thirdValue > low && thirdValue < high)
		{
			// WIN: strictly between — player wins bet from pot
			var winAmount = Math.Min(betAmount, mainPot.Amount);
			activePlayer.ChipStack += winAmount;
			mainPot.Amount -= winAmount;
			result = TurnResult.Win;
			description = $"{activePlayer.Player?.Name ?? "Unknown"} wins {winAmount}";
		}
		else
		{
			// LOSE: outside boundaries — player loses bet to pot
			activePlayer.ChipStack -= betAmount;
			mainPot.Amount += betAmount;
			result = TurnResult.Lose;
			description = $"{activePlayer.Player?.Name ?? "Unknown"} loses {betAmount}";
		}

		// Track first orbit
		state.LastTurnResult = result;
		state.LastTurnDescription = description;
		state.SubPhase = TurnSubPhase.AwaitingResolution;
		state.PlayersCompletedFirstTurn.Add(activePlayer.SeatPosition);
		SetState(game, state);

		game.UpdatedAt = now;
		await context.SaveChangesAsync(cancellationToken);

		return result;
	}

	/// <summary>
	/// Advances to the next player's turn or completes the game if the pot is empty.
	/// </summary>
	internal static async Task AdvanceToNextPlayerOrCompleteAsync(
		CardsDbContext context,
		Game game,
		IHandHistoryRecorder handHistoryRecorder,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// Discard this turn's community cards
		var turnCommunityCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             gc.Location == CardLocation.Community &&
			             !gc.IsDiscarded)
			.ToListAsync(cancellationToken);

		foreach (var card in turnCommunityCards)
		{
			card.IsDiscarded = true;
			card.Location = CardLocation.Discarded;
		}

		// Check pot
		var mainPot = await context.Pots
			.FirstOrDefaultAsync(p => p.GameId == game.Id &&
			                          p.HandNumber == game.CurrentHandNumber &&
			                          p.PotType == PotType.Main,
				cancellationToken);

		if (mainPot is { Amount: <= 0 })
		{
			// Pot is empty — game complete
			await CompleteGameAsync(context, game, mainPot, handHistoryRecorder, now, cancellationToken);
			return;
		}

		// Find next active player
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.IsSittingOut)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		if (activePlayers.Count == 0)
		{
			// No active players — shouldn't happen, but complete the game
			if (mainPot is not null)
				await CompleteGameAsync(context, game, mainPot, handHistoryRecorder, now, cancellationToken);
			return;
		}

		var nextSeat = FindNextActivePlayerSeat(game, activePlayers, game.CurrentPlayerIndex);
		game.CurrentPlayerIndex = nextSeat;

		// Reset turn state for next player
		UpdateState(game, state =>
		{
			state.SubPhase = TurnSubPhase.AwaitingFirstBoundary;
			state.AceIsHigh = null;
			state.BetAmount = 0;
			state.LastTurnResult = TurnResult.None;
			state.DeckRefreshedThisTurn = false;
			state.LastTurnDescription = null;
		});

		game.UpdatedAt = now;
		await context.SaveChangesAsync(cancellationToken);
	}

	/// <summary>
	/// Determines the maximum legal bet for the active player.
	/// </summary>
	internal static int GetMaxLegalBet(Game game, GamePlayer player)
	{
		var state = GetState(game);
		var pot = game.Pots?.FirstOrDefault(p => p.PotType == PotType.Main);
		var potAmount = pot?.Amount ?? 0;

		var isFirstOrbit = !AllPlayersCompletedFirstTurn(game, state);
		var maxBet = isFirstOrbit ? potAmount / 2 : potAmount;

		// Cap at player's chip stack
		return Math.Min(maxBet, player.ChipStack);
	}

	/// <summary>
	/// Returns true if all active players have completed their first turn.
	/// </summary>
	internal static bool AllPlayersCompletedFirstTurn(Game game, InBetweenState state)
	{
		var activeSeats = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.IsSittingOut)
			.Select(gp => gp.SeatPosition)
			.ToHashSet();

		return activeSeats.All(seat => state.PlayersCompletedFirstTurn.Contains(seat));
	}

	#endregion

	#region Showdown (not used — game ends when pot empties)

	/// <inheritdoc />
	public override async Task<ShowdownResult> PerformShowdownAsync(
		CardsDbContext context,
		Game game,
		IHandHistoryRecorder handHistoryRecorder,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// In-Between has no traditional showdown — this is called when pot reaches 0
		return ShowdownResult.Success([], [], 0, "In-Between game complete — pot emptied.");
	}

	/// <inheritdoc />
	public override Task<string> ProcessPostShowdownAsync(
		CardsDbContext context,
		Game game,
		ShowdownResult showdownResult,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		return Task.FromResult(nameof(Phases.Complete));
	}

	#endregion

	#region Auto Actions

	/// <inheritdoc />
	public override async Task PerformAutoActionAsync(AutoActionContext context)
	{
		if (context.CurrentPhase.Equals(nameof(Phases.InBetweenTurn), StringComparison.OrdinalIgnoreCase))
		{
			// Auto-action for timed-out player: pass (bet 0)
			var player = context.Game.GamePlayers
				.FirstOrDefault(gp => gp.SeatPosition == context.PlayerSeatIndex);

			if (player is null) return;

			try
			{
				var command = new Features.Games.InBetween.v1.Commands.PlaceBet.PlaceBetCommand(
					context.GameId, player.PlayerId, 0);
				await context.Mediator.Send(command, context.CancellationToken);
				context.Logger.LogInformation(
					"Auto-pass completed for player {PlayerId} in In-Between game {GameId}",
					player.PlayerId, context.GameId);
			}
			catch (Exception ex)
			{
				context.Logger.LogError(ex,
					"Auto-pass failed for player {PlayerId} in In-Between game {GameId}",
					player.PlayerId, context.GameId);
			}
		}
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Gets the effective numeric value for a card symbol.
	/// Ace value depends on player choice: high (14) or low (1).
	/// Non-ace cards use their enum value.
	/// </summary>
	internal static int GetEffectiveValue(CardSymbol symbol, bool? aceIsHigh)
	{
		if (symbol == CardSymbol.Ace)
			return aceIsHigh == true ? 14 : 1;

		return (int)symbol;
	}

	private static async Task<List<GameCard>> GetAvailableDeckCardsAsync(
		CardsDbContext context,
		Game game,
		CancellationToken cancellationToken)
	{
		return await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             gc.Location == CardLocation.Deck &&
			             gc.GamePlayerId == null &&
			             !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);
	}

	private static async Task RefreshDeckAsync(
		CardsDbContext context,
		Game game,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// Remove all non-deck cards (already dealt/discarded)
		var oldCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             (gc.Location != CardLocation.Community || gc.IsDiscarded))
			.ToListAsync(cancellationToken);

		// Keep currently visible community cards (active turn boundary cards)
		var activeCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             gc.Location == CardLocation.Community &&
			             !gc.IsDiscarded)
			.ToListAsync(cancellationToken);

		var activeCardIds = activeCards.Select(c => c.Id).ToHashSet();

		var cardsToRemove = await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             (gc.IsDiscarded || gc.Location == CardLocation.Deck))
			.ToListAsync(cancellationToken);

		if (cardsToRemove.Count > 0)
			context.GameCards.RemoveRange(cardsToRemove);

		// Create fresh shuffled deck
		var shuffledDeck = CreateShuffledDeck();
		var activeCardSymbols = activeCards
			.Select(c => (c.Suit, c.Symbol))
			.ToHashSet();

		var deckOrder = 0;
		foreach (var (suit, symbol) in shuffledDeck)
		{
			// Skip cards currently in play
			if (activeCardSymbols.Contains(((CardSuit)(int)suit, (CardSymbol)(int)symbol)))
				continue;

			context.GameCards.Add(new GameCard
			{
				GameId = game.Id,
				GamePlayerId = null,
				HandNumber = game.CurrentHandNumber,
				Suit = (CardSuit)(int)suit,
				Symbol = (CardSymbol)(int)symbol,
				DealOrder = deckOrder++,
				Location = CardLocation.Deck,
				IsVisible = false,
				IsDiscarded = false,
				DealtAt = now
			});
		}

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
				Suit = (CardSuit)(int)suit,
				Symbol = (CardSymbol)(int)symbol,
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

	private static int FindNextActivePlayerSeat(Game game, List<GamePlayer> activePlayers, int currentSeat)
	{
		var seats = activePlayers.Select(p => p.SeatPosition).OrderBy(s => s).ToList();
		if (seats.Count == 0) return currentSeat;

		var seatsAfter = seats.Where(s => s > currentSeat).ToList();
		return seatsAfter.Count > 0 ? seatsAfter.First() : seats.First();
	}

	private static async Task CompleteGameAsync(
		CardsDbContext context,
		Game game,
		Data.Entities.Pot mainPot,
		IHandHistoryRecorder handHistoryRecorder,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// Find the player who emptied the pot (current player who just won)
		var winner = game.GamePlayers
			.FirstOrDefault(gp => gp.SeatPosition == game.CurrentPlayerIndex);

		mainPot.IsAwarded = true;
		mainPot.AwardedAt = now;
		if (winner is not null)
		{
			mainPot.WinnerPayouts = JsonSerializer.Serialize(new[]
			{
				new
				{
					playerId = winner.PlayerId.ToString(),
					playerName = winner.Player?.Name ?? "Unknown",
					amount = 0 // pot is already empty — winnings were already transferred
				}
			});
		}

		game.CurrentPhase = nameof(Phases.Complete);
		game.HandCompletedAt = now;
		game.UpdatedAt = now;

		if (game.IsDealersChoice)
		{
			// On DC tables, signal for background service to transition
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
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.ToList();

		await RecordGameCompletionHistoryAsync(
			handHistoryRecorder, game, activePlayers, winner, now, cancellationToken);
	}

	private static async Task RecordGameCompletionHistoryAsync(
		IHandHistoryRecorder handHistoryRecorder,
		Game game,
		List<GamePlayer> activePlayers,
		GamePlayer? winner,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var winnerInfos = winner is not null
			?
			[
				new WinnerInfo
				{
					PlayerId = winner.PlayerId,
					PlayerName = winner.Player?.Name ?? winner.PlayerId.ToString(),
					AmountWon = 0
				}
			]
			: new List<WinnerInfo>();

		var playerResults = activePlayers.Select(gp => new PlayerResultInfo
		{
			PlayerId = gp.PlayerId,
			PlayerName = gp.Player?.Name ?? gp.PlayerId.ToString(),
			SeatPosition = gp.SeatPosition,
			IsWinner = winner is not null && gp.PlayerId == winner.PlayerId,
			NetChipDelta = 0
		}).ToList();

		try
		{
			await handHistoryRecorder.RecordHandHistoryAsync(new RecordHandHistoryParameters
			{
				GameId = game.Id,
				HandNumber = game.CurrentHandNumber,
				CompletedAtUtc = now,
				WonByFold = false,
				TotalPot = 0,
				WinningHandDescription = winner is not null
					? $"{winner.Player?.Name ?? "Unknown"} emptied the pot and wins the In-Between game!"
					: "In-Between game complete.",
				Winners = winnerInfos,
				PlayerResults = playerResults
			}, cancellationToken);
		}
		catch
		{
			// History recording should not block game completion
		}
	}

	#endregion
}
