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

using CardGames.Poker.Api.Services.InMemoryEngine;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;

/// <summary>
/// Handles the <see cref="ProcessDiscardCommand"/> to process discard actions from players
/// during the Irish Hold 'Em discard phase.
/// </summary>
public class ProcessDiscardCommandHandler(CardsDbContext context,
	IOptions<InMemoryEngineOptions> engineOptions,
	IGameStateManager gameStateManager)
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
		{
			return new ProcessDiscardError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = ProcessDiscardErrorCode.GameNotFound
			};
		}

		// 2. Validate game is in discard phase
		if (game.CurrentPhase != nameof(Phases.DrawPhase))
		{
			return new ProcessDiscardError
			{
				Message = $"Cannot process discard. Game is in '{game.CurrentPhase}' phase. " +
				          $"Discard is only allowed during '{nameof(Phases.DrawPhase)}' phase.",
				Code = ProcessDiscardErrorCode.NotInDiscardPhase
			};
		}

		var gameTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code;
		var isPhilsMom = string.Equals(gameTypeCode, PokerGameMetadataRegistry.PhilsMomCode, StringComparison.OrdinalIgnoreCase);
		var isIrishHoldEm = string.Equals(gameTypeCode, PokerGameMetadataRegistry.IrishHoldEmCode, StringComparison.OrdinalIgnoreCase);
		var isCrazyPineapple = string.Equals(gameTypeCode, PokerGameMetadataRegistry.CrazyPineappleCode, StringComparison.OrdinalIgnoreCase);
		if (!isPhilsMom && !isIrishHoldEm && !isCrazyPineapple)
		{
			return new ProcessDiscardError
			{
				Message = $"Discard is not supported for game type '{gameTypeCode}'.",
				Code = ProcessDiscardErrorCode.NotInDiscardPhase
			};
		}

		var requiredDiscardCount = isIrishHoldEm ? IrishRequiredDiscardCount : PhilsMomRequiredDiscardCount;
		if (isCrazyPineapple)
		{
			requiredDiscardCount = CrazyPineappleRequiredDiscardCount;
		}

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
		{
			return new ProcessDiscardError
			{
				Message = "No eligible players to discard.",
				Code = ProcessDiscardErrorCode.NoEligiblePlayers
			};
		}

		var requestedSeatIndex = command.PlayerSeatIndex ?? game.CurrentDrawPlayerIndex;
		var currentDrawPlayer = activePlayers.FirstOrDefault(p => p.SeatPosition == requestedSeatIndex);
		if (currentDrawPlayer is null)
		{
			return new ProcessDiscardError
			{
				Message = "Discard player not found.",
				Code = ProcessDiscardErrorCode.NotPlayerTurn
			};
		}

		if (currentDrawPlayer.HasFolded)
		{
			return new ProcessDiscardError
			{
				Message = "Folded players cannot discard.",
				Code = ProcessDiscardErrorCode.NotPlayerTurn
			};
		}

		// 4. Validate exact discard count for this variant.
		if (command.DiscardIndices.Count != requiredDiscardCount)
		{
			return new ProcessDiscardError
			{
				Message = $"Must discard exactly {requiredDiscardCount} cards. Received {command.DiscardIndices.Count}.",
				Code = ProcessDiscardErrorCode.InvalidDiscardCount
			};
		}

		// 5. Get the player's current hand cards (not discarded)
		var playerCards = game.GameCards
			.Where(gc => gc.GamePlayerId == currentDrawPlayer.Id && !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.ToList();

		if (playerCards.Count < InitialHoleCardCount && isIrishHoldEm)
		{
			return new ProcessDiscardError
			{
				Message = $"Player does not have enough cards. Expected {InitialHoleCardCount}, found {playerCards.Count}.",
				Code = ProcessDiscardErrorCode.InsufficientCards
			};
		}

		if (playerCards.Count < CrazyPineappleInitialHoleCardCount && isCrazyPineapple)
		{
			return new ProcessDiscardError
			{
				Message = $"Player does not have enough cards. Expected {CrazyPineappleInitialHoleCardCount}, found {playerCards.Count}.",
				Code = ProcessDiscardErrorCode.InsufficientCards
			};
		}

		var drawRound = ResolveDrawRound(playerCards.Count, isPhilsMom, isCrazyPineapple);
		if (drawRound < 0)
		{
			return new ProcessDiscardError
			{
				Message = "Player card state is invalid for the current discard round.",
				Code = ProcessDiscardErrorCode.InsufficientCards
			};
		}

		// 6. Validate card indices are within the player's current hand.
		if (command.DiscardIndices.Any(i => i < 0 || i >= playerCards.Count))
		{
			return new ProcessDiscardError
			{
				Message = $"Invalid card index. Indices must be between 0 and {playerCards.Count - 1}.",
				Code = ProcessDiscardErrorCode.InvalidCardIndex
			};
		}

		// 7. Check for duplicate indices
		if (command.DiscardIndices.Distinct().Count() != command.DiscardIndices.Count)
		{
			return new ProcessDiscardError
			{
				Message = "Duplicate card indices are not allowed.",
				Code = ProcessDiscardErrorCode.InvalidCardIndex
			};
		}

		// 8. Check if player has already discarded
		if (currentDrawPlayer.HasDrawnThisRound)
		{
			return new ProcessDiscardError
			{
				Message = "Player has already discarded this round.",
				Code = ProcessDiscardErrorCode.AlreadyDiscarded
			};
		}

		// 9. Process discards — mark cards as discarded (no replacement cards in Irish Hold 'Em)
		var discardedCards = new List<CardInfo>();
		foreach (var index in command.DiscardIndices.Distinct().OrderByDescending(i => i))
		{
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
			if (isCrazyPineapple)
			{
				await StartFlopPhaseAsync(game, activePlayers, now, cancellationToken, dealFlopCards: false);
			}
			else if (isPhilsMom && drawRound == 1)
			{
				await StartFlopPhaseAsync(game, activePlayers, now, cancellationToken);
			}
			else
			{
				// Irish Hold 'Em and Phil's Mom second discard both advance to Turn.
				await StartTurnPhaseAsync(game, activePlayers, now, cancellationToken);
			}
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

		if (engineOptions.Value.Enabled)
			await gameStateManager.GetOrLoadGameAsync(command.GameId, cancellationToken);

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

		if (!pendingPlayers.Any())
		{
			return -1; // All players have discarded
		}

		return pendingPlayers[0].SeatPosition;
	}

	private static int ResolveDrawRound(int cardsInHand, bool isPhilsMom, bool isCrazyPineapple)
	{
		if (isCrazyPineapple)
		{
			return cardsInHand switch
			{
				3 => 1,
				_ => -1
			};
		}

		if (!isPhilsMom)
		{
			return 1;
		}

		return cardsInHand switch
		{
			4 => 1,
			3 => 2,
			_ => -1
		};
	}

	private async Task StartFlopPhaseAsync(
		Game game,
		List<GamePlayer> activePlayers,
		DateTimeOffset now,
		CancellationToken cancellationToken,
		bool dealFlopCards = true)
	{
		foreach (var gamePlayer in activePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		var firstActorIndex = FindFirstActivePlayerAfterDealerForBetting(game, activePlayers);

		if (firstActorIndex < 0)
		{
			game.CurrentPhase = nameof(Phases.Showdown);
			game.CurrentPlayerIndex = -1;
			game.CurrentDrawPlayerIndex = -1;
			return;
		}

		if (dealFlopCards)
		{
			await DealCommunityCardsAsync(game, nameof(Phases.Flop), cardCount: 3, firstDealOrder: 1, now, cancellationToken);
		}

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
	}

	private async Task StartTurnPhaseAsync(Game game, List<GamePlayer> activePlayers, DateTimeOffset now, CancellationToken cancellationToken)
	{
		// Reset draw state
		foreach (var gamePlayer in activePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		// Find first active player after dealer who can bet (not folded, not all-in)
		var firstActorIndex = FindFirstActivePlayerAfterDealerForBetting(game, activePlayers);

		// If no players can bet (all are all-in or folded), skip directly to showdown
		if (firstActorIndex < 0)
		{
			game.CurrentPhase = nameof(Phases.Showdown);
			game.CurrentPlayerIndex = -1;
			game.CurrentDrawPlayerIndex = -1;
			return;
		}

		// Deal the Turn community card (4th community card)
		await DealCommunityCardsAsync(game, nameof(Phases.Turn), cardCount: 1, firstDealOrder: 4, now, cancellationToken);

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
	}

	/// <summary>
	/// Deals community cards for the specified phase from the top of the remaining deck.
	/// </summary>
	private async Task DealCommunityCardsAsync(
		Game game,
		string dealtAtPhase,
		int cardCount,
		int firstDealOrder,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (cardCount <= 0)
		{
			return;
		}

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
	/// Finds the first active player after the dealer who can bet.
	/// Excludes folded and all-in players since they cannot participate in betting.
	/// </summary>
	private static int FindFirstActivePlayerAfterDealerForBetting(Game game, List<GamePlayer> activePlayers)
	{
		var totalPlayers = game.GamePlayers.Count;
		var searchIndex = (game.DealerPosition + 1) % totalPlayers;

		for (var i = 0; i < totalPlayers; i++)
		{
			var player = activePlayers.FirstOrDefault(p => p.SeatPosition == searchIndex);
			if (player is not null && !player.HasFolded && !player.IsAllIn)
			{
				return searchIndex;
			}
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
