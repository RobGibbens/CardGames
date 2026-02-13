using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.FiveCardDraw;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using BettingRound = CardGames.Poker.Api.Data.Entities.BettingRound;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;
using CardGames.Poker.Api.Models;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;

/// <summary>
/// Handles the <see cref="DealHandsCommand"/> to deal five cards to each active player.
/// </summary>
public class DealHandsCommandHandler(CardsDbContext context)
	: IRequestHandler<DealHandsCommand, OneOf<DealHandsSuccessful, DealHandsError>>
{
	private const int CardsPerPlayer = 5;

	/// <inheritdoc />
	public async Task<OneOf<DealHandsSuccessful, DealHandsError>> Handle(
		DealHandsCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new DealHandsError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = DealHandsErrorCode.GameNotFound
			};
		}

		// 2. Validate game state allows dealing
		if (game.CurrentPhase != nameof(Phases.Dealing))
		{
			return new DealHandsError
			{
				Message = $"Cannot deal hands. Game is in '{game.CurrentPhase}' phase. " +
				          $"Hands can only be dealt when the game is in '{nameof(Phases.Dealing)}' phase.",
				Code = DealHandsErrorCode.InvalidGameState
			};
		}

		// 3. Get active players who should receive cards
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.HasFolded && !gp.IsSittingOut)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		// 4. Verify we have enough cards
		var totalCardsNeeded = activePlayers.Count * CardsPerPlayer;
		const int standardDeckSize = 52;
		if (totalCardsNeeded > standardDeckSize)
		{
			return new DealHandsError
			{
				Message = $"Not enough cards to deal. Need {totalCardsNeeded} cards for {activePlayers.Count} players but deck only has {standardDeckSize} cards.",
				Code = DealHandsErrorCode.InsufficientCards
			};
		}

		// 5. Create shuffled deck, persist all 52 cards, then deal from the persisted deck
		var dealer = FrenchDeckDealer.WithFullDeck();
		dealer.Shuffle();

		var shuffledCards = dealer.DealCards(52);
		var deckCards = new List<GameCard>();
		var deckOrder = 1;

		foreach (var card in shuffledCards)
		{
			var gameCard = new GameCard
			{
				GameId = game.Id,
				GamePlayerId = null,
				HandNumber = game.CurrentHandNumber,
				Suit = MapSuit(card.Suit),
				Symbol = MapSymbol(card.Symbol),
				Location = CardLocation.Deck,
				DealOrder = deckOrder++,
				DealtAtPhase = null,
				IsVisible = false,
				IsWild = false,
				IsDiscarded = false,
				IsDrawnCard = false,
				IsBuyCard = false,
				DealtAt = now
			};
			deckCards.Add(gameCard);
			context.GameCards.Add(gameCard);
		}

		// Deal cards to each player from the persisted deck
		var deckIndex = 0;
		var playerHands = new List<PlayerDealtCards>();

		// Sort players starting from left of dealer
		var dealerPosition = game.DealerPosition;
		var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
		var totalSeats = maxSeatPosition + 1;

		var playersInDealOrder = activePlayers
			.OrderBy(p => (p.SeatPosition - dealerPosition - 1 + totalSeats) % totalSeats)
			.ToList();

		foreach (var gamePlayer in playersInDealOrder)
		{
			var playerCards = new List<GameCard>();
			for (var cardIndex = 0; cardIndex < CardsPerPlayer; cardIndex++)
			{
				if (deckIndex >= deckCards.Count) break;

				var deckCard = deckCards[deckIndex++];
				deckCard.GamePlayerId = gamePlayer.Id;
				deckCard.Location = CardLocation.Hole;
				deckCard.DealtAtPhase = nameof(Phases.Dealing);
				deckCard.DealtAt = now;
				playerCards.Add(deckCard);
			}

			// Sort cards by value (descending) then by suit for consistent display order
			var sortedCards = playerCards
				.OrderByDescending(c => GetCardSortValue(c.Symbol))
				.ThenBy(c => GetSuitSortValue(c.Suit))
				.ToList();

			var displayOrder = 1;
			var dealtCards = new List<DealtCard>();
			foreach (var card in sortedCards)
			{
				card.DealOrder = displayOrder;
				dealtCards.Add(new DealtCard
				{
					Suit = card.Suit,
					Symbol = card.Symbol,
					DealOrder = displayOrder
				});
				displayOrder++;
			}

			playerHands.Add(new PlayerDealtCards
			{
				PlayerName = gamePlayer.Player.Name,
				SeatPosition = gamePlayer.SeatPosition,
				Cards = dealtCards
			});
		}

		// 6. Reset current bets for all players before first betting round
		foreach (var gamePlayer in game.GamePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		// 7. Determine first player to act (left of dealer)
		var firstActorIndex = FindFirstActivePlayerAfterDealer(game, activePlayers);

		// 8. Create betting round record
		var bettingRound = new BettingRound
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			RoundNumber = 1,
			Street = nameof(Phases.FirstBettingRound),
			CurrentBet = 0,
			MinBet = game.MinBet ?? 0,
			RaiseCount = 0,
			MaxRaises = 0, // Unlimited raises
			LastRaiseAmount = 0,
			PlayersInHand = activePlayers.Count,
			PlayersActed = 0,
			CurrentActorIndex = firstActorIndex,
			LastAggressorIndex = -1,
			IsComplete = false,
			StartedAt = now
		};

		context.BettingRounds.Add(bettingRound);

		// 9. Update game state - transition to FirstBettingRound phase
		game.CurrentPhase = nameof(Phases.FirstBettingRound);
		game.CurrentPlayerIndex = firstActorIndex;
		game.Status = GameStatus.InProgress;
		game.UpdatedAt = now;

		// 10. Persist changes
		await context.SaveChangesAsync(cancellationToken);

		// Get current player name
		var currentPlayerName = firstActorIndex >= 0
			? activePlayers.FirstOrDefault(p => p.SeatPosition == firstActorIndex)?.Player.Name
			  ?? game.GamePlayers.FirstOrDefault(gp => gp.SeatPosition == firstActorIndex)?.Player.Name
			: null;

		return new DealHandsSuccessful
		{
			GameId = game.Id,
			CurrentPhase = game.CurrentPhase,
			HandNumber = game.CurrentHandNumber,
			CurrentPlayerIndex = firstActorIndex,
			CurrentPlayerName = currentPlayerName,
			PlayerHands = playerHands
		};
	}

	/// <summary>
	/// Finds the first active player after the dealer position.
	/// </summary>
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

		return -1; // No active player found
	}

	/// <summary>
	/// Maps core library Suit to entity CardSuit.
	/// </summary>
	private static CardSuit MapSuit(Suit suit) => suit switch
	{
		Suit.Hearts => CardSuit.Hearts,
		Suit.Diamonds => CardSuit.Diamonds,
		Suit.Spades => CardSuit.Spades,
		Suit.Clubs => CardSuit.Clubs,
		_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit")
	};

	/// <summary>
	/// Maps core library Symbol to entity CardSymbol.
	/// </summary>
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

			/// <summary>
			/// Gets the numeric sort value for a card symbol (Ace high = 14).
			/// </summary>
			private static int GetCardSortValue(CardSymbol symbol) => symbol switch
			{
				CardSymbol.Deuce => 2,
				CardSymbol.Three => 3,
				CardSymbol.Four => 4,
				CardSymbol.Five => 5,
				CardSymbol.Six => 6,
				CardSymbol.Seven => 7,
				CardSymbol.Eight => 8,
				CardSymbol.Nine => 9,
				CardSymbol.Ten => 10,
				CardSymbol.Jack => 11,
				CardSymbol.Queen => 12,
				CardSymbol.King => 13,
				CardSymbol.Ace => 14,
				_ => 0
			};

			/// <summary>
			/// Gets the sort value for a suit (for consistent ordering: Clubs, Diamonds, Hearts, Spades).
			/// </summary>
			private static int GetSuitSortValue(CardSuit suit) => suit switch
			{
				CardSuit.Clubs => 0,
				CardSuit.Diamonds => 1,
				CardSuit.Hearts => 2,
				CardSuit.Spades => 3,
				_ => 0
			};
		}
