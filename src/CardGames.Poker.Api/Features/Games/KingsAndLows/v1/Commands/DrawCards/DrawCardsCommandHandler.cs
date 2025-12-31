using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
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

		// 3. Find the player
		var gamePlayersList = game.GamePlayers.ToList();
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

		// 8. Remove discarded cards
		var discardedCards = new List<GameCard>();
		foreach (var index in discardIndices.OrderByDescending(i => i))
		{
			if (index < playerCards.Count)
			{
				var card = playerCards[index];
				discardedCards.Add(card);
				context.GameCards.Remove(card);
				playerCards.RemoveAt(index);
			}
		}

		// 9. Deal new cards (simulate deck)
		// In a real implementation, we'd track the deck state
		// For now, we'll generate random cards
		var random = new Random();
		var suits = new[] { CardSuit.Hearts, CardSuit.Diamonds, CardSuit.Clubs, CardSuit.Spades };
		var symbols = new[] { CardSymbol.Deuce, CardSymbol.Three, CardSymbol.Four, CardSymbol.Five, 
							  CardSymbol.Six, CardSymbol.Seven, CardSymbol.Eight, CardSymbol.Nine, 
							  CardSymbol.Ten, CardSymbol.Jack, CardSymbol.Queen, CardSymbol.King, CardSymbol.Ace };

		for (int i = 0; i < discardIndices.Count; i++)
		{
			var newCard = new GameCard
			{
				GameId = game.Id,
				GamePlayerId = gamePlayer.Id,
				HandNumber = game.CurrentHandNumber,
				Suit = suits[random.Next(suits.Length)],
				Symbol = symbols[random.Next(symbols.Length)],
				Location = CardLocation.Hole,
				DealOrder = playerCards.Count + i + 1,
				DealtAtPhase = "DrawPhase",
				IsVisible = false,
				DealtAt = now
			};
			context.GameCards.Add(newCard);
			playerCards.Add(newCard);
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

		if (drawPhaseComplete)
		{
			// All staying players have drawn - determine next phase
			var totalStaying = gamePlayersList.Count(gp => gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay);
			
			if (totalStaying == 1)
			{
				// Single player stayed - go to player vs deck
				game.CurrentPhase = nameof(KingsAndLowsPhase.PlayerVsDeck);
			}
			else
			{
				// Multiple players - go to showdown and automatically perform it
				game.CurrentPhase = nameof(KingsAndLowsPhase.Showdown);
				
				// Auto-perform showdown for multiple staying players
				await PerformShowdownAndSetupPotMatching(game, gamePlayersList, context, now, cancellationToken);
			}
			
			nextPhase = game.CurrentPhase;
			game.CurrentDrawPlayerIndex = -1;
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
					nextPlayerId = player.PlayerId;
					break;
				}
				nextIndex = (nextIndex + 1) % gamePlayersList.Count;
				searched++;
			}
		}

		game.UpdatedAt = now;

		// 12. Persist changes
		await context.SaveChangesAsync(cancellationToken);

		return new DrawCardsSuccessful
		{
			GameId = game.Id,
			PlayerId = command.PlayerId,
			CardsDiscarded = discardIndices.Count,
			CardsDrawn = discardIndices.Count,
			DrawPhaseComplete = drawPhaseComplete,
			NextPhase = nextPhase,
			NextPlayerId = nextPlayerId
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

		// Create a new pot for next hand that will receive losers' matching contributions
		var newPot = new Pot
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber + 1,
			Amount = 0,
			CreatedAt = now
		};
		context.Pots.Add(newPot);

		// Transition to PotMatching phase if there are losers
		if (losers.Any())
		{
			game.CurrentPhase = nameof(KingsAndLowsPhase.PotMatching);
		}
		else
		{
			// No losers (all tied?) - just complete
			game.CurrentPhase = nameof(KingsAndLowsPhase.Complete);
		}
	}
}
