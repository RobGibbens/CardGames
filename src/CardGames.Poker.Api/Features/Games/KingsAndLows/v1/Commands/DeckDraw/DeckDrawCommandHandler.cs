using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Games.KingsAndLows;
using CardGames.Poker.Hands.DrawHands;
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
		if (game.CurrentPhase != nameof(Phases.PlayerVsDeck))
		{
			return new DeckDrawError
			{
				Message = $"Cannot process deck draw. Game is in '{game.CurrentPhase}' phase, " +
						  $"but must be in '{nameof(Phases.PlayerVsDeck)}' phase.",
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

		// 5. Get the deck's hand cards (Location = Board with no GamePlayerId)
		var deckCards = game.GameCards
			.Where(gc => gc.GamePlayerId == null && gc.HandNumber == game.CurrentHandNumber && gc.Location == CardLocation.Board)
			.OrderBy(gc => gc.DealOrder)
			.ToList();

		// If deck doesn't have cards yet, deal them from the shared deck
		if (deckCards.Count == 0)
		{
			var cardsInDeck = game.GameCards
				.Where(gc => gc.HandNumber == game.CurrentHandNumber && gc.Location == CardLocation.Deck)
				.OrderBy(gc => gc.DealOrder)
				.Take(5)
				.ToList();

			for (int i = 0; i < cardsInDeck.Count; i++)
			{
				var card = cardsInDeck[i];
				card.GamePlayerId = null; // Deck hand cards have no player
				card.Location = CardLocation.Board; // Board represents deck hand
				card.DealOrder = i + 1;
				card.DealtAtPhase = "PlayerVsDeck";
				card.IsVisible = true; // Deck cards are visible to all
				card.DealtAt = now;
				deckCards.Add(card);
			}
		}

				// 6. Mark discarded cards and track them
				var discardedCards = new List<GameCard>();
				var discardedCardInfos = new List<DeckCardInfo>();
				foreach (var index in discardIndices.OrderByDescending(i => i))
				{
					if (index < deckCards.Count)
					{
						var card = deckCards[index];
						discardedCards.Add(card);
						discardedCardInfos.Add(new DeckCardInfo
						{
							Suit = card.Suit,
							Symbol = card.Symbol,
							Display = FormatCard(card.Symbol, card.Suit)
						});
						// Mark the card as discarded instead of removing it
						card.IsDiscarded = true;
						card.DiscardedAtDrawRound = 1;
						card.Location = CardLocation.Discarded;
						deckCards.RemoveAt(index);
					}
				}
				// Reverse to maintain original order
				discardedCardInfos.Reverse();

				// 7. Deal new cards for the deck from the shared deck
				var newCardInfos = new List<DeckCardInfo>();

				// Get cards still in the deck (not yet dealt to any player or the deck hand)
				// Filter by GamePlayerId == null to ensure we don't grab cards assigned to players
				var remainingDeckCards = await context.GameCards
					.Where(gc => gc.GameId == game.Id && 
					             gc.HandNumber == game.CurrentHandNumber && 
					             gc.Location == CardLocation.Deck &&
					             gc.GamePlayerId == null &&
					             !gc.IsDiscarded)
					.OrderBy(gc => gc.DealOrder)
					.Take(discardIndices.Count)
					.ToListAsync(cancellationToken);

				// Deal cards from the deck to the deck's hand
				for (int i = 0; i < remainingDeckCards.Count; i++)
				{
					var cardFromDeck = remainingDeckCards[i];
					
					// Update the card: move from deck to the deck's hand
					cardFromDeck.GamePlayerId = null;
					cardFromDeck.Location = CardLocation.Board;
					cardFromDeck.DealOrder = deckCards.Count + i + 1;
					cardFromDeck.DealtAtPhase = "PlayerVsDeck";
					cardFromDeck.IsVisible = true;
					cardFromDeck.IsDrawnCard = true;
					cardFromDeck.DrawnAtRound = 1;
					cardFromDeck.DealtAt = now;
					
					deckCards.Add(cardFromDeck);
					newCardInfos.Add(new DeckCardInfo
					{
						Suit = cardFromDeck.Suit,
						Symbol = cardFromDeck.Symbol,
						Display = FormatCard(cardFromDeck.Symbol, cardFromDeck.Suit)
					});
				}

				// 8. Build final hand info and evaluate
				var finalHandInfos = deckCards
					.OrderBy(c => c.DealOrder)
					.Select(c => new DeckCardInfo
					{
						Suit = c.Suit,
						Symbol = c.Symbol,
						Display = FormatCard(c.Symbol, c.Suit)
					})
					.ToList();

				// Evaluate the deck's hand for description
				string? handDescription = null;
				if (deckCards.Count >= 5)
				{
					var coreCards = deckCards
						.Select(c => new Card((Suit)(int)c.Suit, (Symbol)(int)c.Symbol))
						.ToList();
					var kingsAndLowsHand = new KingsAndLowsDrawHand(coreCards);
					handDescription = HandDescriptionFormatter.GetHandDescription(kingsAndLowsHand);
				}

				// 9. Advance to Showdown phase
				game.CurrentPhase = nameof(Phases.Showdown);
				game.UpdatedAt = now;

				// 10. Persist changes
				await context.SaveChangesAsync(cancellationToken);

				return new DeckDrawSuccessful
				{
					GameId = game.Id,
					CardsDiscarded = discardIndices.Count,
					CardsDrawn = discardIndices.Count,
					NextPhase = game.CurrentPhase,
					DiscardedCards = discardedCardInfos,
					NewCards = newCardInfos,
					FinalHand = finalHandInfos,
					HandDescription = handDescription
				};
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

				var suitChar = suit switch
				{
					CardSuit.Hearts => "?",
					CardSuit.Diamonds => "?",
					CardSuit.Spades => "?",
					CardSuit.Clubs => "?",
					_ => "?"
				};

				return $"{symbolStr}{suitChar}";
			}
		}
