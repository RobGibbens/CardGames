using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.SevenCardStud;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using BettingRoundEntity = CardGames.Poker.Api.Data.Entities.BettingRound;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands;

/// <summary>
/// Handles the <see cref="DealHandsCommand"/> to deal cards for the current Seven Card Stud street.
/// </summary>
public class DealHandsCommandHandler(CardsDbContext context)
: IRequestHandler<DealHandsCommand, OneOf<DealHandsSuccessful, DealHandsError>>
{
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

		// 2. Validate game state - Seven Card Stud deals cards during street phases
		var validPhases = new[]
		{
			nameof(Phases.ThirdStreet),
			nameof(Phases.FourthStreet),
			nameof(Phases.FifthStreet),
			nameof(Phases.SixthStreet),
			nameof(Phases.SeventhStreet)
		};

		if (!validPhases.Contains(game.CurrentPhase))
		{
			return new DealHandsError
			{
				Message = $"Cannot deal hands. Game is in '{game.CurrentPhase}' phase. Hands can only be dealt during street phases.",
				Code = DealHandsErrorCode.InvalidGameState
			};
		}

		// 3. Get active players
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.HasFolded)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		// 4. Load all cards already dealt for this hand to exclude from the deck
		var alreadyDealtCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id && gc.HandNumber == game.CurrentHandNumber)
			.Select(gc => new { gc.Suit, gc.Symbol })
			.ToListAsync(cancellationToken);

		// 5. Create shuffled deck and remove already dealt cards
		var dealer = FrenchDeckDealer.WithFullDeck();

		foreach (var dealtCard in alreadyDealtCards)
		{
			var cardToRemove = new Card(MapSuitToDomain(dealtCard.Suit), MapSymbolToDomain(dealtCard.Symbol));
			dealer.DealSpecific(cardToRemove);
		}

		dealer.Shuffle();

		var playerHands = new List<PlayerDealtCards>();

		// 6. Deal cards based on street
		foreach (var gamePlayer in activePlayers)
		{
			var dealtCards = new List<DealtCard>();
			var existingCardCount = await context.GameCards
			.CountAsync(gc => gc.GamePlayerId == gamePlayer.Id &&
							  gc.HandNumber == game.CurrentHandNumber &&
							  !gc.IsDiscarded, cancellationToken);

			var dealOrder = existingCardCount + 1;

			if (game.CurrentPhase == nameof(Phases.ThirdStreet))
			{
				// Deal 2 hole + 1 board
				for (int i = 0; i < 2; i++)
				{
					var card = dealer.DealCard();
					var gameCard = CreateGameCard(game, gamePlayer, card, CardLocation.Hole, dealOrder++, false, now);
					context.GameCards.Add(gameCard);
					dealtCards.Add(new DealtCard { Suit = gameCard.Suit, Symbol = gameCard.Symbol, DealOrder = gameCard.DealOrder });
				}

				var boardCard = dealer.DealCard();
				var boardGameCard = CreateGameCard(game, gamePlayer, boardCard, CardLocation.Board, dealOrder++, true, now);
				context.GameCards.Add(boardGameCard);
				dealtCards.Add(new DealtCard { Suit = boardGameCard.Suit, Symbol = boardGameCard.Symbol, DealOrder = boardGameCard.DealOrder });
			}
			else if (game.CurrentPhase == nameof(Phases.SeventhStreet))
			{
				// Deal 1 hole card
				var card = dealer.DealCard();
				var gameCard = CreateGameCard(game, gamePlayer, card, CardLocation.Hole, dealOrder++, false, now);
				context.GameCards.Add(gameCard);
				dealtCards.Add(new DealtCard { Suit = gameCard.Suit, Symbol = gameCard.Symbol, DealOrder = gameCard.DealOrder });
			}
			else
			{
				// Deal 1 board card (4th, 5th, 6th streets)
				var card = dealer.DealCard();
				var gameCard = CreateGameCard(game, gamePlayer, card, CardLocation.Board, dealOrder++, true, now);
				context.GameCards.Add(gameCard);
				dealtCards.Add(new DealtCard { Suit = gameCard.Suit, Symbol = gameCard.Symbol, DealOrder = gameCard.DealOrder });
			}

				playerHands.Add(new PlayerDealtCards
					{
						PlayerName = gamePlayer.Player.Name,
						SeatPosition = gamePlayer.SeatPosition,
						Cards = dealtCards
					});
				}

				// 7. Reset current bets for all players before betting round
				foreach (var gamePlayer in game.GamePlayers)
				{
					gamePlayer.CurrentBet = 0;
				}

				// 8. Determine first player to act based on street
				int firstActorSeatPosition;
				int currentBet = 0;

				if (game.CurrentPhase == nameof(Phases.ThirdStreet))
				{
					// Third Street: bring-in player is lowest up card
					firstActorSeatPosition = FindBringInPlayer(activePlayers, playerHands);

					// Post the bring-in bet if configured
					var bringIn = game.BringIn ?? 0;
					if (bringIn > 0 && firstActorSeatPosition >= 0)
					{
						var bringInPlayer = activePlayers.FirstOrDefault(p => p.SeatPosition == firstActorSeatPosition);
						if (bringInPlayer is not null)
						{
							var actualBringIn = Math.Min(bringIn, bringInPlayer.ChipStack);
							bringInPlayer.CurrentBet = actualBringIn;
							bringInPlayer.ChipStack -= actualBringIn;
							currentBet = actualBringIn;
						}
					}
				}
				else
				{
					// Other streets: player with best visible hand acts first
					firstActorSeatPosition = FindBestVisibleHandPlayer(activePlayers, game.Id, game.CurrentHandNumber, context);
				}

				// 9. Determine min bet based on street (small bet for 3rd/4th, big bet for 5th/6th/7th)
				var isSmallBetStreet = game.CurrentPhase == nameof(Phases.ThirdStreet) ||
									   game.CurrentPhase == nameof(Phases.FourthStreet);
				var minBet = isSmallBetStreet ? (game.SmallBet ?? game.MinBet ?? 0) : (game.BigBet ?? game.MinBet ?? 0);

				// 10. Create betting round record
				var roundNumber = game.CurrentPhase switch
				{
					nameof(Phases.ThirdStreet) => 1,
					nameof(Phases.FourthStreet) => 2,
					nameof(Phases.FifthStreet) => 3,
					nameof(Phases.SixthStreet) => 4,
					nameof(Phases.SeventhStreet) => 5,
					_ => 1
				};

				var bettingRound = new BettingRoundEntity
				{
					GameId = game.Id,
					HandNumber = game.CurrentHandNumber,
					RoundNumber = roundNumber,
					Street = game.CurrentPhase,
					CurrentBet = currentBet,
					MinBet = minBet,
					RaiseCount = 0,
					MaxRaises = 0, // Unlimited raises
					LastRaiseAmount = 0,
					PlayersInHand = activePlayers.Count,
					PlayersActed = 0,
					CurrentActorIndex = firstActorSeatPosition,
					LastAggressorIndex = -1,
					IsComplete = false,
					StartedAt = now
				};

				context.BettingRounds.Add(bettingRound);

				// 11. Update game state - remain in street phase for betting
				game.CurrentPlayerIndex = firstActorSeatPosition;
				game.BringInPlayerIndex = game.CurrentPhase == nameof(Phases.ThirdStreet) ? firstActorSeatPosition : -1;
				game.Status = GameStatus.InProgress;
				game.UpdatedAt = now;

				// 12. Persist changes
				await context.SaveChangesAsync(cancellationToken);

				// Get current player name
				var currentPlayerName = firstActorSeatPosition >= 0
					? activePlayers.FirstOrDefault(p => p.SeatPosition == firstActorSeatPosition)?.Player.Name
					: null;

				return new DealHandsSuccessful
				{
					GameId = game.Id,
					CurrentPhase = game.CurrentPhase,
					HandNumber = game.CurrentHandNumber,
					CurrentPlayerIndex = firstActorSeatPosition,
					CurrentPlayerName = currentPlayerName,
					PlayerHands = playerHands
				};
			}

			/// <summary>
			/// Finds the player with the lowest up card for bring-in determination.
			/// In case of tie, use suit order: clubs (lowest), diamonds, hearts, spades (highest).
			/// </summary>
			private static int FindBringInPlayer(List<GamePlayer> activePlayers, List<PlayerDealtCards> playerHands)
			{
				int lowestSeatPosition = -1;
				DealtCard? lowestCard = null;

				foreach (var playerHand in playerHands)
				{
					// The last card dealt on Third Street is the up card (board card)
					var upCard = playerHand.Cards.LastOrDefault();
					if (upCard is null)
					{
						continue;
					}

					if (lowestCard is null || CompareCardsForBringIn(upCard, lowestCard) < 0)
					{
						lowestCard = upCard;
						lowestSeatPosition = playerHand.SeatPosition;
					}
				}

				return lowestSeatPosition;
			}

			/// <summary>
			/// Compares two cards for bring-in determination.
			/// Lower value is "worse". For ties, use suit order (clubs lowest, spades highest).
			/// </summary>
			private static int CompareCardsForBringIn(DealtCard a, DealtCard b)
			{
				var aValue = GetCardValue(a.Symbol);
				var bValue = GetCardValue(b.Symbol);

				if (aValue != bValue)
				{
					return aValue.CompareTo(bValue);
				}

				// Suit order for ties: Clubs (0) < Diamonds (1) < Hearts (2) < Spades (3)
				return GetSuitRank(a.Suit).CompareTo(GetSuitRank(b.Suit));
			}

			private static int GetCardValue(CardSymbol symbol) => symbol switch
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

			private static int GetSuitRank(CardSuit suit) => suit switch
			{
				CardSuit.Clubs => 0,
				CardSuit.Diamonds => 1,
				CardSuit.Hearts => 2,
				CardSuit.Spades => 3,
				_ => 0
			};

			/// <summary>
			/// Finds the player with the best visible hand for first action on 4th+ streets.
			/// For simplicity, uses highest visible card (can be enhanced for pair detection).
			/// </summary>
			private static int FindBestVisibleHandPlayer(List<GamePlayer> activePlayers, Guid gameId, int handNumber, CardsDbContext context)
			{
				int bestSeatPosition = activePlayers.FirstOrDefault()?.SeatPosition ?? 0;
				int highestValue = -1;
				CardSuit highestSuit = CardSuit.Clubs;

				foreach (var player in activePlayers)
				{
					// Get visible board cards for this player
					var boardCards = context.GameCards
						.Where(gc => gc.GamePlayerId == player.Id &&
									 gc.HandNumber == handNumber &&
									 gc.Location == CardLocation.Board &&
									 gc.IsVisible &&
									 !gc.IsDiscarded)
						.ToList();

					foreach (var card in boardCards)
					{
						var cardValue = GetCardValue(card.Symbol);
						if (cardValue > highestValue || (cardValue == highestValue && GetSuitRank(card.Suit) > GetSuitRank(highestSuit)))
						{
							highestValue = cardValue;
							highestSuit = card.Suit;
							bestSeatPosition = player.SeatPosition;
						}
					}
				}

				return bestSeatPosition;
			}

	private static GameCard CreateGameCard(Game game, GamePlayer gamePlayer, Card card, CardLocation location, int dealOrder, bool isVisible, DateTimeOffset now)
	{
		return new GameCard
		{
			GameId = game.Id,
			GamePlayerId = gamePlayer.Id,
			HandNumber = game.CurrentHandNumber,
			Suit = MapSuit(card.Suit),
			Symbol = MapSymbol(card.Symbol),
			Location = location,
			DealOrder = dealOrder,
			DealtAtPhase = game.CurrentPhase,
			IsVisible = isVisible,
			IsWild = false,
			IsDiscarded = false,
			IsDrawnCard = false,
			IsBuyCard = false,
			DealtAt = now
		};
	}

	private static CardSuit MapSuit(Suit suit) => suit switch
	{
		Suit.Hearts => CardSuit.Hearts,
		Suit.Diamonds => CardSuit.Diamonds,
		Suit.Spades => CardSuit.Spades,
		Suit.Clubs => CardSuit.Clubs,
		_ => throw new ArgumentOutOfRangeException(nameof(suit))
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
			_ => throw new ArgumentOutOfRangeException(nameof(symbol))
		};

		private static Suit MapSuitToDomain(CardSuit suit) => suit switch
		{
			CardSuit.Hearts => Suit.Hearts,
			CardSuit.Diamonds => Suit.Diamonds,
			CardSuit.Spades => Suit.Spades,
			CardSuit.Clubs => Suit.Clubs,
			_ => throw new ArgumentOutOfRangeException(nameof(suit))
		};

		private static Symbol MapSymbolToDomain(CardSymbol symbol) => symbol switch
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
			_ => throw new ArgumentOutOfRangeException(nameof(symbol))
		};
	}
