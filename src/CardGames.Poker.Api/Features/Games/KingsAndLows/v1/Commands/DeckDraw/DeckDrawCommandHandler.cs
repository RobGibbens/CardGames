using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.KingsAndLows;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DeckDraw;

/// <summary>
/// Handles the <see cref="DeckDrawCommand"/> to process the deck's draw in player-vs-deck scenario.
/// </summary>
public class DeckDrawCommandHandler(CardsDbContext context)
	: IRequestHandler<DeckDrawCommand, OneOf<DeckDrawSuccessful, DeckDrawError>>
{
	private const int MaxDiscards = 5;

	public async Task<OneOf<DeckDrawSuccessful, DeckDrawError>> Handle(
		DeckDrawCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players and cards
		var game = await context.Games
			.Include(g => g.GamePlayers)
			.Include(g => g.GameCards)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new DeckDrawError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = DeckDrawErrorCode.GameNotFound
			};
		}

		// 2. Validate game is in PlayerVsDeck phase
		if (game.CurrentPhase != nameof(KingsAndLowsPhase.PlayerVsDeck))
		{
			return new DeckDrawError
			{
				Message = $"Cannot process deck draw. Game is in '{game.CurrentPhase}' phase, " +
						  $"but must be in '{nameof(KingsAndLowsPhase.PlayerVsDeck)}' phase.",
				Code = DeckDrawErrorCode.InvalidPhase
			};
		}

		// 3. Find the player
		var gamePlayer = game.GamePlayers.FirstOrDefault(gp => gp.PlayerId == command.PlayerId);
		if (gamePlayer is null)
		{
			return new DeckDrawError
			{
				Message = $"Player with ID '{command.PlayerId}' is not in this game.",
				Code = DeckDrawErrorCode.PlayerNotFound
			};
		}

		// 4. Validate discard indices
		var discardIndices = command.DiscardIndices.ToList();
		if (discardIndices.Any(i => i < 0 || i >= 5))
		{
			return new DeckDrawError
			{
				Message = "Invalid card indices. Indices must be between 0 and 4.",
				Code = DeckDrawErrorCode.InvalidDiscardIndices
			};
		}

		if (discardIndices.Count > MaxDiscards)
		{
			return new DeckDrawError
			{
				Message = $"Too many cards to discard. Maximum is {MaxDiscards}.",
				Code = DeckDrawErrorCode.TooManyDiscards
			};
		}

		// 5. Get or create deck's hand cards
		// For the deck, we use a special GamePlayerId of Guid.Empty or create special "Deck" cards
		var deckCards = game.GameCards
			.Where(gc => gc.GamePlayerId == null && gc.HandNumber == game.CurrentHandNumber)
			.OrderBy(gc => gc.DealOrder)
			.ToList();

		// If deck doesn't have cards yet, deal them now
		if (deckCards.Count == 0)
		{
			var random = new Random();
			var suits = new[] { CardSuit.Hearts, CardSuit.Diamonds, CardSuit.Clubs, CardSuit.Spades };
			var symbols = new[] { CardSymbol.Deuce, CardSymbol.Three, CardSymbol.Four, CardSymbol.Five, 
								  CardSymbol.Six, CardSymbol.Seven, CardSymbol.Eight, CardSymbol.Nine, 
								  CardSymbol.Ten, CardSymbol.Jack, CardSymbol.Queen, CardSymbol.King, CardSymbol.Ace };

			for (int i = 0; i < 5; i++)
			{
				var deckCard = new GameCard
				{
					GameId = game.Id,
					GamePlayerId = null, // Deck cards have no player
					HandNumber = game.CurrentHandNumber,
					Suit = suits[random.Next(suits.Length)],
					Symbol = symbols[random.Next(symbols.Length)],
					Location = CardLocation.Board, // Board represents deck hand
					DealOrder = i + 1,
					DealtAtPhase = "PlayerVsDeck",
					IsVisible = true, // Deck cards are visible to all
					DealtAt = now
				};
				context.GameCards.Add(deckCard);
				deckCards.Add(deckCard);
			}
		}

		// 6. Remove discarded cards
		var discardedCards = new List<GameCard>();
		foreach (var index in discardIndices.OrderByDescending(i => i))
		{
			if (index < deckCards.Count)
			{
				var card = deckCards[index];
				discardedCards.Add(card);
				context.GameCards.Remove(card);
				deckCards.RemoveAt(index);
			}
		}

		// 7. Deal new cards for the deck
		var random2 = new Random();
		var suits2 = new[] { CardSuit.Hearts, CardSuit.Diamonds, CardSuit.Clubs, CardSuit.Spades };
		var symbols2 = new[] { CardSymbol.Deuce, CardSymbol.Three, CardSymbol.Four, CardSymbol.Five, 
							   CardSymbol.Six, CardSymbol.Seven, CardSymbol.Eight, CardSymbol.Nine, 
							   CardSymbol.Ten, CardSymbol.Jack, CardSymbol.Queen, CardSymbol.King, CardSymbol.Ace };

		for (int i = 0; i < discardIndices.Count; i++)
		{
			var newCard = new GameCard
			{
				GameId = game.Id,
				GamePlayerId = null,
				HandNumber = game.CurrentHandNumber,
				Suit = suits2[random2.Next(suits2.Length)],
				Symbol = symbols2[random2.Next(symbols2.Length)],
				Location = CardLocation.Board,
				DealOrder = deckCards.Count + i + 1,
				DealtAtPhase = "PlayerVsDeck",
				IsVisible = true,
				DealtAt = now
			};
			context.GameCards.Add(newCard);
			deckCards.Add(newCard);
		}

		// 8. Advance to Showdown phase
		game.CurrentPhase = nameof(KingsAndLowsPhase.Showdown);
		game.UpdatedAt = now;

		// 9. Persist changes
		await context.SaveChangesAsync(cancellationToken);

		return new DeckDrawSuccessful
		{
			GameId = game.Id,
			CardsDiscarded = discardIndices.Count,
			CardsDrawn = discardIndices.Count,
			NextPhase = game.CurrentPhase
		};
	}
}
