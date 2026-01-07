using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Games.KingsAndLows;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards;

/// <summary>
/// Handles the <see cref="DrawCardsCommand"/> to process a player's draw action in Kings and Lows.
/// </summary>
public class DrawCardsCommandHandler(CardsDbContext context)
	: IRequestHandler<DrawCardsCommand, OneOf<DrawCardsSuccessful, DrawCardsError>>
{
	private const int MaxDiscards = 5; // Kings and Lows allows all 5 cards to be discarded

	public async Task<OneOf<DrawCardsSuccessful, DrawCardsError>> Handle(
		DrawCardsCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players and cards
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameCards)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new DrawCardsError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = DrawCardsErrorCode.GameNotFound
			};
		}

		// 2. Validate game is in DrawPhase
		if (game.CurrentPhase != nameof(KingsAndLowsPhase.DrawPhase))
		{
			return new DrawCardsError
			{
				Message = $"Cannot draw cards. Game is in '{game.CurrentPhase}' phase, " +
						  $"but must be in '{nameof(KingsAndLowsPhase.DrawPhase)}' phase.",
				Code = DrawCardsErrorCode.InvalidPhase
			};
		}

		// 3. Find the player (order by SeatPosition for consistent indexing)
		var gamePlayersList = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
		var gamePlayer = gamePlayersList.FirstOrDefault(gp => gp.PlayerId == command.PlayerId);
		if (gamePlayer is null)
		{
			return new DrawCardsError
			{
				Message = $"Player with ID '{command.PlayerId}' is not in this game.",
				Code = DrawCardsErrorCode.PlayerNotFound
			};
		}

		// 4. Verify it's this player's turn to draw
		if (game.CurrentDrawPlayerIndex < 0 ||
			game.CurrentDrawPlayerIndex >= gamePlayersList.Count ||
			gamePlayersList[game.CurrentDrawPlayerIndex].PlayerId != command.PlayerId)
		{
			return new DrawCardsError
			{
				Message = "It is not this player's turn to draw.",
				Code = DrawCardsErrorCode.NotPlayerTurn
			};
		}

		// 5. Check if player has already drawn
		if (gamePlayer.HasDrawnThisRound)
		{
			return new DrawCardsError
			{
				Message = "Player has already drawn this round.",
				Code = DrawCardsErrorCode.PlayerHasAlreadyDrawn
			};
		}

		// 6. Validate discard indices
		var discardIndices = command.DiscardIndices.ToList();
		if (discardIndices.Any(i => i < 0 || i >= 5))
		{
			return new DrawCardsError
			{
				Message = "Invalid card indices. Indices must be between 0 and 4.",
				Code = DrawCardsErrorCode.InvalidDiscardIndices
			};
		}

		if (discardIndices.Count > MaxDiscards)
		{
			return new DrawCardsError
			{
				Message = $"Too many cards to discard. Maximum is {MaxDiscards} in Kings and Lows.",
				Code = DrawCardsErrorCode.TooManyDiscards
			};
		}

		// 7. Get player's current cards
		var playerCards = game.GameCards
			.Where(gc => gc.GamePlayerId == gamePlayer.Id && gc.HandNumber == game.CurrentHandNumber)
			.OrderBy(gc => gc.DealOrder)
			.ToList();

		// 8. Mark discarded cards and track them for response
		var discardedGameCards = new List<GameCard>();
		var discardedCardInfos = new List<CardInfo>();
		foreach (var index in discardIndices.OrderByDescending(i => i))
		{
			if (index < playerCards.Count)
			{
				var card = playerCards[index];
				discardedGameCards.Add(card);
				discardedCardInfos.Add(new CardInfo
				{
					Suit = card.Suit,
					Symbol = card.Symbol,
					Display = FormatCard(card.Symbol, card.Suit)
				});
				// Mark the card as discarded instead of removing it
				card.IsDiscarded = true;
				card.DiscardedAtDrawRound = 1;
				card.Location = CardLocation.Discarded;
				playerCards.RemoveAt(index);
			}
		}
		// Reverse to maintain original order
		discardedCardInfos.Reverse();

		// 9. Deal new cards from the shared deck
		// Get cards still in the deck (not yet dealt to any player)
		var deckCards = game.GameCards
			.Where(gc => gc.HandNumber == game.CurrentHandNumber && gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToList();

		if (deckCards.Count < discardIndices.Count)
		{
			return new DrawCardsError
			{
				Message = $"Not enough cards remaining in the deck. Need {discardIndices.Count} but only {deckCards.Count} available.",
				Code = DrawCardsErrorCode.InsufficientCards
			};
		}

		var newCardInfos = new List<CardInfo>();
		for (int i = 0; i < discardIndices.Count; i++)
		{
			var cardFromDeck = deckCards[i];
			
			// Update the card: move from deck to player's hand
			cardFromDeck.GamePlayerId = gamePlayer.Id;
			cardFromDeck.Location = CardLocation.Hand;
			cardFromDeck.DealOrder = playerCards.Count + i + 1;
			cardFromDeck.DealtAtPhase = "DrawPhase";
			cardFromDeck.IsVisible = true;
			cardFromDeck.IsDrawnCard = true;
			cardFromDeck.DrawnAtRound = 1; // First draw round
			cardFromDeck.DealtAt = now;
			
			playerCards.Add(cardFromDeck);
			newCardInfos.Add(new CardInfo
			{
				Suit = cardFromDeck.Suit,
				Symbol = cardFromDeck.Symbol,
				Display = FormatCard(cardFromDeck.Symbol, cardFromDeck.Suit)
			});
		}

		// 10. Mark player as having drawn
		gamePlayer.HasDrawnThisRound = true;

		// 11. Find next player who needs to draw (staying players who haven't drawn)
		var stayingPlayers = gamePlayersList
			.Where(gp => gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay && !gp.HasDrawnThisRound)
			.ToList();

		bool drawPhaseComplete = stayingPlayers.Count == 0;
		string? nextPhase = null;
		Guid? nextPlayerId = null;
		string? nextPlayerName = null;

		if (drawPhaseComplete)
		{
			// All staying players have drawn - determine next phase
			var totalStaying = gamePlayersList.Count(gp => gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay);

			if (totalStaying == 1)
			{
				// Single player stayed - go to player vs deck
				game.CurrentPhase = nameof(KingsAndLowsPhase.PlayerVsDeck);

				// Deal the deck's hand now so it's visible in the overlay
				await DealDeckHandAsync(game, context, now, cancellationToken);
			}
			else
			{
				// Multiple players - go to DrawComplete phase first
				// The ContinuousPlayBackgroundService will transition to Showdown after a delay
				// so all players can see their new cards
				game.CurrentPhase = nameof(KingsAndLowsPhase.DrawComplete);
				game.DrawCompletedAt = now;
				game.UpdatedAt = now;

				// Save changes so new cards are in database and state is broadcast
				await context.SaveChangesAsync(cancellationToken);

				// Note: Showdown will be performed by ContinuousPlayBackgroundService after delay
			}

				nextPhase = game.CurrentPhase;
				game.CurrentDrawPlayerIndex = -1;
				game.CurrentPlayerIndex = -1;
			}
		else
		{
			// Find next player to draw (circular, starting after current player)
			var currentIndex = game.CurrentDrawPlayerIndex;
			var nextIndex = (currentIndex + 1) % gamePlayersList.Count;
			var searched = 0;

			while (searched < gamePlayersList.Count)
			{
				var player = gamePlayersList[nextIndex];
				if (player.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay && !player.HasDrawnThisRound)
				{
					game.CurrentDrawPlayerIndex = nextIndex;
					game.CurrentPlayerIndex = player.SeatPosition;
					nextPlayerId = player.PlayerId;
					nextPlayerName = player.Player?.Name;
					break;
				}
				nextIndex = (nextIndex + 1) % gamePlayersList.Count;
				searched++;
			}
		}

			// 12. Persist changes
			game.UpdatedAt = now;
			await context.SaveChangesAsync(cancellationToken);

			return new DrawCardsSuccessful
			{
				GameId = game.Id,
				PlayerId = command.PlayerId,
				PlayerName = gamePlayer.Player?.Name,
				PlayerSeatIndex = gamePlayer.SeatPosition,
				CardsDiscarded = discardIndices.Count,
				CardsDrawn = discardIndices.Count,
				DiscardedCards = discardedCardInfos,
				NewCards = newCardInfos,
				DrawPhaseComplete = drawPhaseComplete,
				NextPhase = nextPhase,
				NextPlayerId = nextPlayerId,
				NextPlayerName = nextPlayerName
			};
		}

	/// <summary>
	/// Performs showdown, determines winner/losers, distributes pot, and transitions to PotMatching phase.
	/// </summary>
	private static async Task PerformShowdownAndSetupPotMatching(
		Game game,
		List<GamePlayer> gamePlayersList,
		CardsDbContext context,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// Get all cards for staying players
		var stayingPlayers = gamePlayersList.Where(gp => gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay).ToList();

		// Load main pot for current hand
		var mainPot = await context.Pots
			.FirstOrDefaultAsync(p => p.GameId == game.Id && p.HandNumber == game.CurrentHandNumber, cancellationToken);

		if (mainPot == null)
		{
			// No pot to distribute - just complete the hand
			game.CurrentPhase = nameof(KingsAndLowsPhase.Complete);
			game.HandCompletedAt = now;
			game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
			MoveDealer(game);
			return;
		}

		// Evaluate hands using the KingsAndLowsDrawHand evaluator
		var playerHandEvaluations = new List<(GamePlayer player, long strength)>();

		foreach (var player in stayingPlayers)
		{
			var playerCards = context.GameCards
				.Where(gc => gc.GamePlayerId == player.Id && gc.HandNumber == game.CurrentHandNumber)
				.OrderBy(gc => gc.DealOrder)
				.Select(gc => new { gc.Suit, gc.Symbol })
				.ToList();

			if (playerCards.Count == 5)
			{
				// Convert to domain Card objects for evaluation
				var cards = playerCards.Select(c => new CardGames.Core.French.Cards.Card(
					(CardGames.Core.French.Cards.Suit)(int)c.Suit,
					(CardGames.Core.French.Cards.Symbol)(int)c.Symbol
				)).ToList();

				var hand = new CardGames.Poker.Hands.DrawHands.KingsAndLowsDrawHand(cards);
				playerHandEvaluations.Add((player, hand.Strength));
			}
		}

		// Find winner(s)
		if (playerHandEvaluations.Count == 0)
		{
			game.CurrentPhase = nameof(KingsAndLowsPhase.Complete);
			game.HandCompletedAt = now;
			game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
			MoveDealer(game);
			return;
		}

		var maxStrength = playerHandEvaluations.Max(h => h.strength);
		var winners = playerHandEvaluations.Where(h => h.strength == maxStrength).Select(h => h.player).ToList();
		var losers = stayingPlayers.Where(p => !winners.Contains(p)).ToList();

		// Distribute pot to winner(s)
		var potAmount = mainPot.Amount;
		var sharePerWinner = potAmount / winners.Count;
		var remainder = potAmount % winners.Count;

		foreach (var winner in winners)
		{
			var payout = sharePerWinner;
			if (remainder > 0)
			{
				payout++;
				remainder--;
			}
			winner.ChipStack += payout;
		}

		// Clear the current pot (winners took it)
		mainPot.Amount = 0;

		// Auto-perform pot matching: losers must match the pot for the next hand
		var matchAmount = potAmount; // The amount each loser must match
		var totalMatched = 0;

		foreach (var loser in losers)
		{
			// Loser matches the pot (or goes all-in if insufficient chips)
			var actualMatch = Math.Min(matchAmount, loser.ChipStack);
			loser.ChipStack -= actualMatch;
			totalMatched += actualMatch;
		}

		// Create a new pot for next hand with the matched contributions
		if (totalMatched > 0)
		{
			var newPot = new Pot
			{
				GameId = game.Id,
				HandNumber = game.CurrentHandNumber + 1,
				PotType = Data.Entities.PotType.Main,
				PotOrder = 0,
				Amount = totalMatched,
				IsAwarded = false,
				CreatedAt = now
			};
			context.Pots.Add(newPot);
		}

		// Complete the hand
		game.CurrentPhase = nameof(KingsAndLowsPhase.Complete);
		game.HandCompletedAt = now;
		game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
		MoveDealer(game);
	}

	/// <summary>
	/// Formats a card symbol and suit into a display string.
	/// </summary>
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
			CardSuit.Hearts => "?",
			CardSuit.Diamonds => "?",
			CardSuit.Spades => "?",
			CardSuit.Clubs => "?",
			_ => "?"
		};

		return $"{symbolStr}{suitStr}";
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

			// Find next occupied seat clockwise from current position
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

		/// <summary>
		/// Deals a 5-card hand for the deck in player vs deck scenario.
		/// Uses cards from the persisted shuffled deck.
		/// </summary>
		private static async Task DealDeckHandAsync(
			Game game,
			CardsDbContext dbContext,
			DateTimeOffset now,
			CancellationToken cancellationToken)
		{
			// Get cards still in the deck (not yet dealt to any player)
			var deckCards = await dbContext.GameCards
				.Where(gc => gc.GameId == game.Id && 
				             gc.HandNumber == game.CurrentHandNumber && 
				             gc.Location == CardLocation.Deck)
				.OrderBy(gc => gc.DealOrder)
				.Take(5)
				.ToListAsync(cancellationToken);

			// Deal 5 cards from the deck to "the deck's hand" (GamePlayerId = null, Location = Board)
			for (int i = 0; i < deckCards.Count; i++)
			{
				var card = deckCards[i];
				card.GamePlayerId = null; // Deck cards have no player
				card.Location = CardLocation.Board; // Board represents deck hand
				card.DealOrder = i + 1;
				card.DealtAtPhase = nameof(KingsAndLowsPhase.PlayerVsDeck);
				card.IsVisible = true; // Deck cards are visible to all
				card.DealtAt = now;
			}
		}
	}
