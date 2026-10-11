using System.Text.Json;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using BettingRound = CardGames.Poker.Api.Data.Entities.BettingRound;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;

/// <summary>
///     Handles the <see cref="ProcessDiscardCommand" /> to process discard actions from players
///     during the Irish Hold 'Em discard phase.
/// </summary>
public class ProcessDiscardCommandHandler(CardsDbContext context)
	: IRequestHandler<ProcessDiscardCommand, OneOf<ProcessDiscardSuccessful, ProcessDiscardError>>
{
	private const int InitialHoleCardCount = 4;
	private const int CrazyPineappleInitialHoleCardCount = 3;
	private const int IrishRequiredDiscardCount = 2;
	private const int PhilsMomRequiredDiscardCount = 1;
	private const int CrazyPineappleRequiredDiscardCount = 1;

	/// <inheritdoc />
	public async Task<OneOf<ProcessDiscardSuccessful, ProcessDiscardError>> Handle(
		ProcessDiscardCommand command,
		CancellationToken cancellationToken)
	{
		context.ChangeTracker.Clear();

		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players and cards
		var game = await context.Games
			.Include(g => g.GamePlayers)
			.ThenInclude(gp => gp.Player)
			.Include(g => g.GameType)
			.Include(g => g.GameCards.Where(gc => gc.HandNumber == context.Games
				.Where(g2 => g2.Id == command.GameId)
				.Select(g2 => g2.CurrentHandNumber)
				.FirstOrDefault() && !gc.IsDiscarded))
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
			return new ProcessDiscardError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = ProcessDiscardErrorCode.GameNotFound
			};

		// 2. Validate game is in discard phase
		if (game.CurrentPhase != nameof(Phases.DrawPhase))
			return new ProcessDiscardError
			{
				Message = $"Cannot process discard. Game is in '{game.CurrentPhase}' phase. " +
				          $"Discard is only allowed during '{nameof(Phases.DrawPhase)}' phase.",
				Code = ProcessDiscardErrorCode.NotInDiscardPhase
			};

		var gameTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code;
		var isPhilsMom = string.Equals(gameTypeCode, PokerGameMetadataRegistry.PhilsMomCode,
			StringComparison.OrdinalIgnoreCase);
		var isIrishHoldEm = string.Equals(gameTypeCode, PokerGameMetadataRegistry.IrishHoldEmCode,
			StringComparison.OrdinalIgnoreCase);
		var isCrazyPineapple = string.Equals(gameTypeCode, PokerGameMetadataRegistry.CrazyPineappleCode,
			StringComparison.OrdinalIgnoreCase);
		if (!isPhilsMom && !isIrishHoldEm && !isCrazyPineapple)
			return new ProcessDiscardError
			{
				Message = $"Discard is not supported for game type '{gameTypeCode}'.",
				Code = ProcessDiscardErrorCode.NotInDiscardPhase
			};

		var requiredDiscardCount = isIrishHoldEm ? IrishRequiredDiscardCount : PhilsMomRequiredDiscardCount;
		if (isCrazyPineapple) requiredDiscardCount = CrazyPineappleRequiredDiscardCount;

		// 3. Get eligible discard players
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		var eligiblePlayers = activePlayers
			.Where(gp => !gp.HasFolded && !gp.HasDrawnThisRound)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		if (eligiblePlayers.Count == 0)
			return new ProcessDiscardError
			{
				Message = "No eligible players to discard.",
				Code = ProcessDiscardErrorCode.NoEligiblePlayers
			};

		var requestedSeatIndex = command.PlayerSeatIndex ?? game.CurrentDrawPlayerIndex;
		var currentDrawPlayer = activePlayers.FirstOrDefault(p => p.SeatPosition == requestedSeatIndex);
		if (currentDrawPlayer is null)
			return new ProcessDiscardError
			{
				Message = "Discard player not found.",
				Code = ProcessDiscardErrorCode.NotPlayerTurn
			};

		if (currentDrawPlayer.HasFolded)
			return new ProcessDiscardError
			{
				Message = "Folded players cannot discard.",
				Code = ProcessDiscardErrorCode.NotPlayerTurn
			};

		// 4. Validate exact discard count for this variant.
		if (command.DiscardIndices.Count != requiredDiscardCount)
			return new ProcessDiscardError
			{
				Message =
					$"Must discard exactly {requiredDiscardCount} cards. Received {command.DiscardIndices.Count}.",
				Code = ProcessDiscardErrorCode.InvalidDiscardCount
			};

		// 5. Get the player's current hand cards (not discarded)
		var playerCards = game.GameCards
			.Where(gc => gc.GamePlayerId == currentDrawPlayer.Id && !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.ToList();

		if (playerCards.Count < InitialHoleCardCount && isIrishHoldEm)
			return new ProcessDiscardError
			{
				Message =
					$"Player does not have enough cards. Expected {InitialHoleCardCount}, found {playerCards.Count}.",
				Code = ProcessDiscardErrorCode.InsufficientCards
			};

		if (playerCards.Count < CrazyPineappleInitialHoleCardCount && isCrazyPineapple)
			return new ProcessDiscardError
			{
				Message =
					$"Player does not have enough cards. Expected {CrazyPineappleInitialHoleCardCount}, found {playerCards.Count}.",
				Code = ProcessDiscardErrorCode.InsufficientCards
			};

		var drawRound = ResolveDrawRound(playerCards.Count, isPhilsMom, isCrazyPineapple);
		if (drawRound < 0)
			return new ProcessDiscardError
			{
				Message = "Player card state is invalid for the current discard round.",
				Code = ProcessDiscardErrorCode.InsufficientCards
			};

		// 6. Validate card indices are within the player's current hand.
		if (command.DiscardIndices.Any(i => i < 0 || i >= playerCards.Count))
			return new ProcessDiscardError
			{
				Message = $"Invalid card index. Indices must be between 0 and {playerCards.Count - 1}.",
				Code = ProcessDiscardErrorCode.InvalidCardIndex
			};

		// 7. Check for duplicate indices
		if (command.DiscardIndices.Distinct().Count() != command.DiscardIndices.Count)
			return new ProcessDiscardError
			{
				Message = "Duplicate card indices are not allowed.",
				Code = ProcessDiscardErrorCode.InvalidCardIndex
			};

		// 8. Check if player has already discarded
		if (currentDrawPlayer.HasDrawnThisRound)
			return new ProcessDiscardError
			{
				Message = "Player has already discarded this round.",
				Code = ProcessDiscardErrorCode.AlreadyDiscarded
			};

		// 9. Process discards — mark cards as discarded (no replacement cards in Irish Hold 'Em)
		var discardedCards = new List<CardInfo>();
		foreach (var index in command.DiscardIndices.Distinct().OrderByDescending(i => i))
			if (index < playerCards.Count)
			{
				var cardToDiscard = playerCards[index];
				cardToDiscard.IsDiscarded = true;
				cardToDiscard.DiscardedAtDrawRound = drawRound;

				discardedCards.Add(new CardInfo
				{
					Suit = cardToDiscard.Suit,
					Symbol = cardToDiscard.Symbol,
					Display = FormatCard(cardToDiscard.Symbol, cardToDiscard.Suit)
				});
			}

		// Mark that the current player has discarded this round
		currentDrawPlayer.HasDrawnThisRound = true;

		// Reverse to maintain original order
		discardedCards.Reverse();

		// 10. Move to next pending discard player or advance phase
		var nextDiscardPlayerIndex = FindNextPendingDiscardPlayer(activePlayers);
		var discardComplete = nextDiscardPlayerIndex < 0;
		string? nextPlayerName = null;

		if (discardComplete)
		{
			// Return before SaveChangesAsync when the advance fails. The discard is only in the
			// change tracker; not saving drops it so a short deck does not persist a partial runout.
			ProcessDiscardError? advanceError;
			if (isCrazyPineapple)
				advanceError = await StartFlopPhaseAsync(game, activePlayers, now, cancellationToken, false);
			else if (isPhilsMom && drawRound == 1)
				advanceError = await StartFlopPhaseAsync(game, activePlayers, now, cancellationToken);
			else
				// Irish Hold 'Em and Phil's Mom second discard both advance to Turn.
				advanceError = await StartTurnPhaseAsync(game, activePlayers, now, cancellationToken);

			if (advanceError is not null)
				return advanceError;
		}
		else
		{
			game.CurrentDrawPlayerIndex = nextDiscardPlayerIndex;
			game.CurrentPlayerIndex = nextDiscardPlayerIndex;
			nextPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == nextDiscardPlayerIndex)?.Player.Name;
		}

		// 11. Update timestamps
		game.UpdatedAt = now;

		// 12. Persist changes
		await context.SaveChangesAsync(cancellationToken);

		return new ProcessDiscardSuccessful
		{
			GameId = game.Id,
			PlayerName = currentDrawPlayer.Player.Name,
			PlayerSeatIndex = currentDrawPlayer.SeatPosition,
			DiscardedCards = discardedCards,
			DiscardPhaseComplete = discardComplete,
			CurrentPhase = game.CurrentPhase,
			NextDiscardPlayerIndex = nextDiscardPlayerIndex,
			NextDiscardPlayerName = nextPlayerName
		};
	}

	private static int FindNextPendingDiscardPlayer(List<GamePlayer> activePlayers)
	{
		var pendingPlayers = activePlayers
			.Where(p => !p.HasFolded && !p.HasDrawnThisRound)
			.OrderBy(p => p.SeatPosition)
			.ToList();

		if (!pendingPlayers.Any()) return -1; // All players have discarded

		return pendingPlayers[0].SeatPosition;
	}

	private static int ResolveDrawRound(int cardsInHand, bool isPhilsMom, bool isCrazyPineapple)
	{
		if (isCrazyPineapple)
			return cardsInHand switch
			{
				3 => 1,
				_ => -1
			};

		if (!isPhilsMom) return 1;

		return cardsInHand switch
		{
			4 => 1,
			3 => 2,
			_ => -1
		};
	}

	private async Task<ProcessDiscardError?> StartFlopPhaseAsync(
		Game game,
		List<GamePlayer> activePlayers,
		DateTimeOffset now,
		CancellationToken cancellationToken,
		bool dealFlopCards = true)
	{
		foreach (var gamePlayer in activePlayers) gamePlayer.CurrentBet = 0;

		// Fewer than two players who can bet means deal the streets this method owes, or open Phil's Mom's second discard; do not skip the deal.
		if (dealFlopCards)
		{
			var flopAlreadyDealt = await context.GameCards.AnyAsync(
				gc => gc.GameId == game.Id
				      && gc.HandNumber == game.CurrentHandNumber
				      && gc.Location == CardLocation.Community
				      && gc.DealtAtPhase == nameof(Phases.Flop),
				cancellationToken);
			if (!flopAlreadyDealt)
				await DealCommunityCardsAsync(game, nameof(Phases.Flop), 3, 1, now, cancellationToken);
		}

		if (FewerThanTwoCanBet(activePlayers) && dealFlopCards)
		{
			foreach (var player in activePlayers)
				player.HasDrawnThisRound = false;

			var firstDrawPlayerIndex = FindFirstPlayerInHandAfterDealer(game, activePlayers);
			if (firstDrawPlayerIndex < 0)
				return new ProcessDiscardError
				{
					Message = "No eligible player remains to discard.",
					Code = ProcessDiscardErrorCode.NoEligiblePlayers
				};

			game.CurrentPhase = nameof(Phases.DrawPhase);
			game.CurrentDrawPlayerIndex = firstDrawPlayerIndex;
			game.CurrentPlayerIndex = firstDrawPlayerIndex;
			return null;
		}

		if (FewerThanTwoCanBet(activePlayers))
		{
			if (!await RunOutTurnAndRiverAsync(game, now, cancellationToken))
				return InsufficientRunoutCards();

			return null;
		}

		var firstActorIndex = FindFirstActivePlayerAfterDealerForBetting(game, activePlayers);
		if (firstActorIndex < 0)
			return new ProcessDiscardError
			{
				Message = "No eligible player can act on the flop.",
				Code = ProcessDiscardErrorCode.NoEligiblePlayers
			};

		var nextRoundNumber = await context.BettingRounds
			.Where(br => br.GameId == game.Id && br.HandNumber == game.CurrentHandNumber)
			.MaxAsync(br => (int?)br.RoundNumber, cancellationToken) ?? 0;

		var bettingRound = new BettingRound
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			RoundNumber = nextRoundNumber + 1,
			Street = nameof(Phases.Flop),
			CurrentBet = 0,
			MinBet = game.MinBet ?? 0,
			RaiseCount = 0,
			MaxRaises = 0,
			LastRaiseAmount = 0,
			PlayersInHand = activePlayers.Count(p => !p.HasFolded),
			PlayersActed = 0,
			CurrentActorIndex = firstActorIndex,
			LastAggressorIndex = -1,
			IsComplete = false,
			StartedAt = now
		};

		context.BettingRounds.Add(bettingRound);

		game.CurrentPhase = nameof(Phases.Flop);
		game.CurrentPlayerIndex = firstActorIndex;
		game.CurrentDrawPlayerIndex = -1;
		return null;
	}

	private async Task<ProcessDiscardError?> StartTurnPhaseAsync(Game game, List<GamePlayer> activePlayers, DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		foreach (var gamePlayer in activePlayers) gamePlayer.CurrentBet = 0;

		// Fewer than two players who can bet means deal the streets this method owes, or open Phil's Mom's second discard; do not skip the deal.
		if (FewerThanTwoCanBet(activePlayers))
		{
			if (!await RunOutTurnAndRiverAsync(game, now, cancellationToken))
				return InsufficientRunoutCards();

			return null;
		}

		var firstActorIndex = FindFirstActivePlayerAfterDealerForBetting(game, activePlayers);
		if (firstActorIndex < 0)
			return new ProcessDiscardError
			{
				Message = "No eligible player can act on the turn.",
				Code = ProcessDiscardErrorCode.NoEligiblePlayers
			};

		// Deal the Turn community card (4th community card)
		await DealCommunityCardsAsync(game, nameof(Phases.Turn), 1, 4, now,
			cancellationToken);

		var nextRoundNumber = await context.BettingRounds
			.Where(br => br.GameId == game.Id && br.HandNumber == game.CurrentHandNumber)
			.MaxAsync(br => (int?)br.RoundNumber, cancellationToken) ?? 0;

		// Create betting round record for Turn
		var bettingRound = new BettingRound
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			RoundNumber = nextRoundNumber + 1,
			Street = nameof(Phases.Turn),
			CurrentBet = 0,
			MinBet = game.MinBet ?? 0,
			RaiseCount = 0,
			MaxRaises = 0,
			LastRaiseAmount = 0,
			PlayersInHand = activePlayers.Count(p => !p.HasFolded),
			PlayersActed = 0,
			CurrentActorIndex = firstActorIndex,
			LastAggressorIndex = -1,
			IsComplete = false,
			StartedAt = now
		};

		context.BettingRounds.Add(bettingRound);

		// Update game state — advance to Turn
		game.CurrentPhase = nameof(Phases.Turn);
		game.CurrentPlayerIndex = firstActorIndex;
		game.CurrentDrawPlayerIndex = -1;
		return null;
	}

	private static bool FewerThanTwoCanBet(List<GamePlayer> activePlayers)
	{
		return activePlayers.Count(p => !p.HasFolded && !p.IsAllIn && p.ChipStack > 0) < 2;
	}

	private static ProcessDiscardError InsufficientRunoutCards()
	{
		return new ProcessDiscardError
		{
			Message = "Not enough cards remain to deal the turn and river.",
			Code = ProcessDiscardErrorCode.InsufficientCards
		};
	}

	/// <summary>
	///     Deals a missing turn and river from one deck read. Returns false when a required street has no card left.
	/// </summary>
	private async Task<bool> RunOutTurnAndRiverAsync(
		Game game,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var dealtPhases = await context.GameCards
			.Where(gc => gc.GameId == game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Community)
			.Select(gc => gc.DealtAtPhase)
			.ToListAsync(cancellationToken);

		var trackedPhases = context.GameCards.Local
			.Where(gc => gc.GameId == game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Community)
			.Select(gc => gc.DealtAtPhase);
		dealtPhases.AddRange(trackedPhases);

		var hasTurn = dealtPhases.Contains("Turn");
		var hasRiver = dealtPhases.Contains("River");

		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		deckCards = deckCards.Where(gc => gc.Location == CardLocation.Deck).ToList();

		var cardsNeeded = (hasTurn ? 0 : 1) + (hasRiver ? 0 : 1);
		if (deckCards.Count < cardsNeeded)
			return false;

		var deckIndex = 0;
		if (!hasTurn)
			MoveDeckCardToStreet(deckCards[deckIndex++], "Turn", 4, now);
		if (!hasRiver)
			MoveDeckCardToStreet(deckCards[deckIndex], "River", 5, now);

		var existingSettings = string.IsNullOrEmpty(game.GameSettings)
			? new Dictionary<string, object>()
			: JsonSerializer.Deserialize<Dictionary<string, object>>(game.GameSettings) ??
			  new Dictionary<string, object>();

		existingSettings["allInRunout"] = true;
		existingSettings["runoutStreets"] = new List<string> { "Turn", "River" };
		existingSettings["runoutHandNumber"] = game.CurrentHandNumber;
		existingSettings["runoutTimestamp"] = now.ToString("O");
		game.GameSettings = JsonSerializer.Serialize(existingSettings);

		game.CurrentPhase = nameof(Phases.Showdown);
		game.CurrentPlayerIndex = -1;
		game.CurrentDrawPlayerIndex = -1;
		return true;
	}

	private static void MoveDeckCardToStreet(GameCard card, string dealtAtPhase, int dealOrder, DateTimeOffset now)
	{
		card.Location = CardLocation.Community;
		card.GamePlayerId = null;
		card.IsVisible = true;
		card.DealtAtPhase = dealtAtPhase;
		card.DealOrder = dealOrder;
		card.DealtAt = now;
	}

	/// <summary>
	///     First seat still in the hand, left of the dealer. All-in seats still discard.
	/// </summary>
	private static int FindFirstPlayerInHandAfterDealer(Game game, List<GamePlayer> activePlayers)
	{
		var totalPlayers = game.GamePlayers.Count;
		var searchIndex = (game.DealerPosition + 1) % totalPlayers;

		for (var i = 0; i < totalPlayers; i++)
		{
			var player = activePlayers.FirstOrDefault(p => p.SeatPosition == searchIndex);
			if (player is not null && !player.HasFolded) return searchIndex;

			searchIndex = (searchIndex + 1) % totalPlayers;
		}

		return -1;
	}

	/// <summary>
	///     Deals community cards for the specified phase from the top of the remaining deck.
	/// </summary>
	private async Task DealCommunityCardsAsync(
		Game game,
		string dealtAtPhase,
		int cardCount,
		int firstDealOrder,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (cardCount <= 0) return;

		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		for (var i = 0; i < cardCount && i < deckCards.Count; i++)
		{
			var card = deckCards[i];
			card.Location = CardLocation.Community;
			card.GamePlayerId = null;
			card.IsVisible = true;
			card.DealtAtPhase = dealtAtPhase;
			card.DealOrder = firstDealOrder + i;
			card.DealtAt = now;
		}
	}

	/// <summary>
	///     Finds the first active player after the dealer who can bet.
	///     Excludes folded and all-in players since they cannot participate in betting.
	/// </summary>
	private static int FindFirstActivePlayerAfterDealerForBetting(Game game, List<GamePlayer> activePlayers)
	{
		var totalPlayers = game.GamePlayers.Count;
		var searchIndex = (game.DealerPosition + 1) % totalPlayers;

		for (var i = 0; i < totalPlayers; i++)
		{
			var player = activePlayers.FirstOrDefault(p => p.SeatPosition == searchIndex);
			if (player is not null && !player.HasFolded && !player.IsAllIn) return searchIndex;

			searchIndex = (searchIndex + 1) % totalPlayers;
		}

		return -1;
	}

	private static string FormatCard(CardSymbol symbol, CardSuit suit)
	{
		var symbolStr = symbol switch
		{
			CardSymbol.Ace => "A",
			CardSymbol.King => "K",
			CardSymbol.Queen => "Q",
			CardSymbol.Jack => "J",
			CardSymbol.Ten => "10",
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

		var suitStr = suit switch
		{
			CardSuit.Hearts => "♥",
			CardSuit.Diamonds => "♦",
			CardSuit.Spades => "♠",
			CardSuit.Clubs => "♣",
			_ => "?"
		};

		return $"{symbolStr}{suitStr}";
	}
}