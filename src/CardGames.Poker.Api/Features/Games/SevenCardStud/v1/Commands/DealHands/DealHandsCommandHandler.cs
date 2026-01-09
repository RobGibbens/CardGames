using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.SevenCardStud;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
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

		// 4. Create shuffled deck
		var dealer = FrenchDeckDealer.WithFullDeck();
		dealer.Shuffle();

		var playerHands = new List<PlayerDealtCards>();

		// 5. Deal cards based on street
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

		game.UpdatedAt = now;
		await context.SaveChangesAsync(cancellationToken);

		return new DealHandsSuccessful
		{
			GameId = game.Id,
			CurrentPhase = game.CurrentPhase,
			HandNumber = game.CurrentHandNumber,
			CurrentPlayerIndex = game.CurrentPlayerIndex,
			CurrentPlayerName = activePlayers.ElementAtOrDefault(game.CurrentPlayerIndex)?.Player.Name,
			PlayerHands = playerHands
		};
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
}
