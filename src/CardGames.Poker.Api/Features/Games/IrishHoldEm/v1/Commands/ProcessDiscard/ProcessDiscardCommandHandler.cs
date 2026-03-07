using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using BettingRound = CardGames.Poker.Api.Data.Entities.BettingRound;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;

/// <summary>
/// Handles the <see cref="ProcessDiscardCommand"/> to process discard actions from players
/// during the Irish Hold 'Em discard phase.
/// </summary>
public class ProcessDiscardCommandHandler(CardsDbContext context)
	: IRequestHandler<ProcessDiscardCommand, OneOf<ProcessDiscardSuccessful, ProcessDiscardError>>
{
	private const int HoleCardCount = 4;
	private const int RequiredDiscardCount = 2;

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

		// 3. Get the current draw/discard player
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		if (game.CurrentDrawPlayerIndex < 0)
		{
			return new ProcessDiscardError
			{
				Message = "No eligible players to discard.",
				Code = ProcessDiscardErrorCode.NoEligiblePlayers
			};
		}

		var currentDrawPlayer = activePlayers.FirstOrDefault(p => p.SeatPosition == game.CurrentDrawPlayerIndex);
		if (currentDrawPlayer is null)
		{
			return new ProcessDiscardError
			{
				Message = "Current discard player not found.",
				Code = ProcessDiscardErrorCode.NotPlayerTurn
			};
		}

		// 4. Validate exactly 2 discard indices
		if (command.DiscardIndices.Count != RequiredDiscardCount)
		{
			return new ProcessDiscardError
			{
				Message = $"Must discard exactly {RequiredDiscardCount} cards. Received {command.DiscardIndices.Count}.",
				Code = ProcessDiscardErrorCode.InvalidDiscardCount
			};
		}

		// 5. Validate card indices are within bounds (0-3 for 4 hole cards)
		if (command.DiscardIndices.Any(i => i < 0 || i >= HoleCardCount))
		{
			return new ProcessDiscardError
			{
				Message = $"Invalid card index. Indices must be between 0 and {HoleCardCount - 1}.",
				Code = ProcessDiscardErrorCode.InvalidCardIndex
			};
		}

		// 6. Check for duplicate indices
		if (command.DiscardIndices.Distinct().Count() != command.DiscardIndices.Count)
		{
			return new ProcessDiscardError
			{
				Message = "Duplicate card indices are not allowed.",
				Code = ProcessDiscardErrorCode.InvalidCardIndex
			};
		}

		// 7. Get the player's current hand cards (not discarded)
		var playerCards = game.GameCards
			.Where(gc => gc.GamePlayerId == currentDrawPlayer.Id && !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.ToList();

		if (playerCards.Count < HoleCardCount)
		{
			return new ProcessDiscardError
			{
				Message = $"Player does not have enough cards. Expected {HoleCardCount}, found {playerCards.Count}.",
				Code = ProcessDiscardErrorCode.InsufficientCards
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
				cardToDiscard.DiscardedAtDrawRound = 1;

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

		// 10. Move to next discard player or advance phase
		var nextDiscardPlayerIndex = FindNextDiscardPlayer(game, activePlayers, currentDrawPlayer.SeatPosition);
		var discardComplete = nextDiscardPlayerIndex < 0;
		string? nextPlayerName = null;

		if (discardComplete)
		{
			// All players have discarded — advance to Turn phase
			StartTurnPhase(game, activePlayers, now);
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

	private static int FindNextDiscardPlayer(Game game, List<GamePlayer> activePlayers, int currentIndex)
	{
		// Find active players who haven't folded and haven't discarded this round
		var eligiblePlayers = activePlayers
			.Where(p => !p.HasFolded && !p.HasDrawnThisRound)
			.OrderBy(p => p.SeatPosition)
			.ToList();

		if (!eligiblePlayers.Any())
		{
			return -1; // All players have discarded
		}

		// Find the next eligible player after currentIndex
		var next = eligiblePlayers.FirstOrDefault(p => p.SeatPosition > currentIndex);
		if (next != null)
		{
			return next.SeatPosition;
		}

		// Wrap around to the first eligible
		return eligiblePlayers.First().SeatPosition;
	}

	private void StartTurnPhase(Game game, List<GamePlayer> activePlayers, DateTimeOffset now)
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

		// Create betting round record for Turn
		var bettingRound = new BettingRound
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			RoundNumber = 3, // PreFlop=1, Flop=2, Turn=3
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
