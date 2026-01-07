using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.FiveCardDraw;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using BettingRound = CardGames.Poker.Api.Data.Entities.BettingRound;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.ProcessDraw;

/// <summary>
/// Handles the <see cref="ProcessDrawCommand"/> to process draw actions from players.
/// </summary>
public class ProcessDrawCommandHandler(CardsDbContext context)
	: IRequestHandler<ProcessDrawCommand, OneOf<ProcessDrawSuccessful, ProcessDrawError>>
{
	private const int HandSize = 5;

	/// <inheritdoc />
	public async Task<OneOf<ProcessDrawSuccessful, ProcessDrawError>> Handle(
		ProcessDrawCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players and cards
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameCards.Where(gc => gc.HandNumber == context.Games
				.Where(g2 => g2.Id == command.GameId)
				.Select(g2 => g2.CurrentHandNumber)
				.FirstOrDefault() && !gc.IsDiscarded))
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new ProcessDrawError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = ProcessDrawErrorCode.GameNotFound
			};
		}

		// 2. Validate game is in draw phase
		if (game.CurrentPhase != nameof(FiveCardDrawPhase.DrawPhase))
		{
			return new ProcessDrawError
			{
				Message = $"Cannot process draw. Game is in '{game.CurrentPhase}' phase. " +
				          $"Draw is only allowed during '{nameof(FiveCardDrawPhase.DrawPhase)}' phase.",
				Code = ProcessDrawErrorCode.NotInDrawPhase
			};
		}

		// 3. Get the current draw player
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		if (game.CurrentDrawPlayerIndex < 0)
		{
			return new ProcessDrawError
			{
				Message = "No eligible players to draw.",
				Code = ProcessDrawErrorCode.NoEligiblePlayers
			};
		}

		var currentDrawPlayer = activePlayers.FirstOrDefault(p => p.SeatPosition == game.CurrentDrawPlayerIndex);
		if (currentDrawPlayer is null)
		{
			return new ProcessDrawError
			{
				Message = "Current draw player not found.",
				Code = ProcessDrawErrorCode.NotPlayerTurn
			};
		}

		// 4. Validate card indices are within bounds
		if (command.DiscardIndices.Any(i => i < 0 || i >= HandSize))
		{
			return new ProcessDrawError
			{
				Message = $"Invalid card index. Indices must be between 0 and {HandSize - 1}.",
				Code = ProcessDrawErrorCode.InvalidCardIndex
			};
		}

		// 5. Get the player's current hand cards (not discarded)
		var playerCards = game.GameCards
			.Where(gc => gc.GamePlayerId == currentDrawPlayer.Id && !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.ToList();

		if (playerCards.Count < HandSize)
		{
			return new ProcessDrawError
			{
				Message = $"Player does not have enough cards. Expected {HandSize}, found {playerCards.Count}.",
				Code = ProcessDrawErrorCode.InvalidCardIndex
			};
		}

		// 6. Validate discard count - allow 4 discards if player has an Ace
		var hasAce = playerCards.Any(c => c.Symbol == CardSymbol.Ace);
		var maxDiscards = hasAce ? 4 : 3;

		if (command.DiscardIndices.Count > maxDiscards)
		{
			return new ProcessDrawError
			{
				Message = $"Cannot discard more than {maxDiscards} cards.",
				Code = ProcessDrawErrorCode.TooManyDiscards
			};
		}

		// 7. Process discards - mark cards as discarded
		var discardedCards = new List<CardInfo>();
		foreach (var index in command.DiscardIndices.Distinct().OrderByDescending(i => i))
		{
			if (index < playerCards.Count)
			{
				var cardToDiscard = playerCards[index];
				cardToDiscard.IsDiscarded = true;
				cardToDiscard.DiscardedAtDrawRound = 1; // First draw round in Five Card Draw

				discardedCards.Add(new CardInfo
				{
					Suit = cardToDiscard.Suit,
					Symbol = cardToDiscard.Symbol,
					Display = FormatCard(cardToDiscard.Symbol, cardToDiscard.Suit)
				});
			}
		}

		// Mark that the current player has drawn this round
		currentDrawPlayer.HasDrawnThisRound = true;

		// Reverse to maintain original order
		discardedCards.Reverse();

		// 8. Deal new cards from deck
		var newCards = new List<CardInfo>();
		if (command.DiscardIndices.Count > 0)
		{
			// Get all cards already dealt in this hand to exclude from new deals
			var usedCards = await context.GameCards
				.Where(gc => gc.GameId == game.Id && gc.HandNumber == game.CurrentHandNumber)
				.Select(gc => new { gc.Suit, gc.Symbol })
				.ToListAsync(cancellationToken);

			var usedCardSet = usedCards.Select(c => (c.Suit, c.Symbol)).ToHashSet();

			// Create a dealer with remaining cards
			var dealer = FrenchDeckDealer.WithFullDeck();
			dealer.Shuffle();

			var cardsDealt = 0;
			var maxDealOrder = playerCards.Max(c => c.DealOrder);

			while (cardsDealt < command.DiscardIndices.Count)
			{
				var card = dealer.DealCards(1).First();
				var mappedSuit = MapSuit(card.Suit);
				var mappedSymbol = MapSymbol(card.Symbol);

				// Skip if card was already used
				if (usedCardSet.Contains((mappedSuit, mappedSymbol)))
				{
					continue;
				}

				// Create new card entity
				var newGameCard = new GameCard
				{
					GameId = game.Id,
					GamePlayerId = currentDrawPlayer.Id,
					HandNumber = game.CurrentHandNumber,
					Suit = mappedSuit,
					Symbol = mappedSymbol,
					Location = CardLocation.Hole,
					DealOrder = ++maxDealOrder,
					DealtAtPhase = nameof(FiveCardDrawPhase.DrawPhase),
					IsVisible = false,
					IsWild = false,
					IsDiscarded = false,
					IsDrawnCard = true,
					DrawnAtRound = 1,
					IsBuyCard = false,
					DealtAt = now
				};

				context.GameCards.Add(newGameCard);
				usedCardSet.Add((mappedSuit, mappedSymbol));

				newCards.Add(new CardInfo
				{
					Suit = mappedSuit,
					Symbol = mappedSymbol,
					Display = FormatCard(mappedSymbol, mappedSuit)
				});

				cardsDealt++;
			}
		}

		// 9. Move to next draw player or advance phase
		var nextDrawPlayerIndex = FindNextDrawPlayer(game, activePlayers, currentDrawPlayer.SeatPosition);
		var drawComplete = nextDrawPlayerIndex < 0;
		string? nextPlayerName = null;

		if (drawComplete)
		{
			// All players have drawn - start second betting round
			StartSecondBettingRound(game, activePlayers, now);
		}
		else
		{
			game.CurrentDrawPlayerIndex = nextDrawPlayerIndex;
			game.CurrentPlayerIndex = nextDrawPlayerIndex;
			nextPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == nextDrawPlayerIndex)?.Player.Name;
		}

			// 10. Update timestamps
			game.UpdatedAt = now;

			// 11. Persist changes
			await context.SaveChangesAsync(cancellationToken);

			return new ProcessDrawSuccessful
			{
				GameId = game.Id,
				PlayerName = currentDrawPlayer.Player.Name,
				PlayerSeatIndex = currentDrawPlayer.SeatPosition,
				DiscardedCards = discardedCards,
				NewCards = newCards,
				DrawComplete = drawComplete,
				CurrentPhase = game.CurrentPhase,
				NextDrawPlayerIndex = nextDrawPlayerIndex,
				NextDrawPlayerName = nextPlayerName
			};
		}

	private static int FindNextDrawPlayer(Game game, List<GamePlayer> activePlayers, int currentIndex)
	{
		// Only consider active players who have not folded, are not all-in, and have not drawn this round
		var eligiblePlayers = activePlayers
			.Where(p => !p.HasFolded && !p.IsAllIn && !p.HasDrawnThisRound)
			.OrderBy(p => p.SeatPosition)
			.ToList();

		if (!eligiblePlayers.Any())
		{
			return -1; // All players have drawn
		}

		// Find the next eligible player after currentIndex
		var next = eligiblePlayers.FirstOrDefault(p => p.SeatPosition > currentIndex);
		if (next != null)
		{
			return next.SeatPosition;
		}

		// If none found after currentIndex, wrap around to the first eligible
		return eligiblePlayers.First().SeatPosition;
	}

	private void StartSecondBettingRound(Game game, List<GamePlayer> activePlayers, DateTimeOffset now)
	{
		// Reset current bets for new betting round
		foreach (var gamePlayer in activePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		// Find first active player after dealer
		var firstActorIndex = FindFirstActivePlayerAfterDealer(game, activePlayers);

		// Create betting round record
		var bettingRound = new BettingRound
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			RoundNumber = 2,
			Street = nameof(FiveCardDrawPhase.SecondBettingRound),
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

		// Update game state
		game.CurrentPhase = nameof(FiveCardDrawPhase.SecondBettingRound);
		game.CurrentPlayerIndex = firstActorIndex;
		game.CurrentDrawPlayerIndex = -1;
	}

	private static int FindFirstActivePlayerAfterDealer(Game game, List<GamePlayer> activePlayers)
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
			CardSuit.Hearts => "?",
			CardSuit.Diamonds => "?",
			CardSuit.Spades => "?",
			CardSuit.Clubs => "?",
			_ => "?"
		};

		return $"{symbolStr}{suitStr}";
	}

	private static CardSuit MapSuit(Suit suit) => suit switch
	{
		Suit.Hearts => CardSuit.Hearts,
		Suit.Diamonds => CardSuit.Diamonds,
		Suit.Spades => CardSuit.Spades,
		Suit.Clubs => CardSuit.Clubs,
		_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit")
	};

	private static CardSymbol MapSymbol(Symbol symbol) => symbol switch
	{
		Symbol.Deuce => CardSymbol.Deuce,
		Symbol.Three => CardSymbol.Three,
		Symbol.Four => CardSymbol.Four,
		Symbol.Five => CardSymbol.Five,
		Symbol.Six => CardSymbol.Six,
		Symbol.Seven => CardSymbol.Seven,
		Symbol.Eight => CardSymbol.Eight,
		Symbol.Nine => CardSymbol.Nine,
		Symbol.Ten => CardSymbol.Ten,
		Symbol.Jack => CardSymbol.Jack,
		Symbol.Queen => CardSymbol.Queen,
		Symbol.King => CardSymbol.King,
		Symbol.Ace => CardSymbol.Ace,
		_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown symbol")
	};
}
