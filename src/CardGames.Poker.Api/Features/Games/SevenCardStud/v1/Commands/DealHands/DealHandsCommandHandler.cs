using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.SevenCardStud;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using BettingRoundEntity = CardGames.Poker.Api.Data.Entities.BettingRound;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands;

/// <summary>
/// Handles the <see cref="DealHandsCommand"/> to deal cards for the current Seven Card Stud street.
/// </summary>
public class DealHandsCommandHandler(
	CardsDbContext context,
	ILogger<DealHandsCommandHandler> logger)
	: IRequestHandler<DealHandsCommand, OneOf<DealHandsSuccessful, DealHandsError>>
{
	/// <inheritdoc />
	public async Task<OneOf<DealHandsSuccessful, DealHandsError>> Handle(
		DealHandsCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		logger.LogDebug("DealHandsCommand starting for game {GameId}", command.GameId);

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

		logger.LogDebug(
			"Game loaded for dealing: Phase {Phase}, Hand {HandNumber}, Players {PlayerCount}",
			game.CurrentPhase, game.CurrentHandNumber, game.GamePlayers.Count);

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
			logger.LogWarning(
				"Invalid phase for dealing: {Phase}. Valid phases: {ValidPhases}",
				game.CurrentPhase, string.Join(", ", validPhases));

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

		logger.LogDebug(
			"Active players for dealing: {ActiveCount} (total: {TotalCount})",
			activePlayers.Count, game.GamePlayers.Count);

		// 4. Load remaining deck cards (pre-shuffled during StartHand)
		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
						 gc.HandNumber == game.CurrentHandNumber &&
						 gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		if (deckCards.Count == 0)
		{
			return new DealHandsError
			{
				Message = "No deck cards available. The deck may not have been created when the hand started.",
				Code = DealHandsErrorCode.InvalidGameState
			};
		}

		var deckIndex = 0;

		var playerHands = new List<PlayerDealtCards>();

		// 5. Deal cards based on street
		foreach (var gamePlayer in activePlayers)
		{
			var dealtCards = new List<DealtCard>();
			var existingCardCount = await context.GameCards
				.CountAsync(gc => gc.GamePlayerId == gamePlayer.Id &&
								  gc.HandNumber == game.CurrentHandNumber &&
								  gc.Location != CardLocation.Deck &&
								  !gc.IsDiscarded, cancellationToken);

			var playerDealOrder = existingCardCount + 1;

			if (game.CurrentPhase == nameof(Phases.ThirdStreet))
			{
				// Deal 2 hole + 1 board
				for (int i = 0; i < 2; i++)
				{
					if (deckIndex >= deckCards.Count)
					{
						return new DealHandsError
						{
							Message = "Not enough cards in deck to complete dealing.",
							Code = DealHandsErrorCode.InvalidGameState
						};
					}

					var gameCard = deckCards[deckIndex++];
					AssignCardToPlayer(gameCard, gamePlayer, CardLocation.Hole, playerDealOrder++, false, game.CurrentPhase, now);
					dealtCards.Add(new DealtCard { Suit = gameCard.Suit, Symbol = gameCard.Symbol, DealOrder = gameCard.DealOrder });
				}

				if (deckIndex >= deckCards.Count)
				{
					return new DealHandsError
					{
						Message = "Not enough cards in deck to complete dealing.",
						Code = DealHandsErrorCode.InvalidGameState
					};
				}

				var boardGameCard = deckCards[deckIndex++];
				AssignCardToPlayer(boardGameCard, gamePlayer, CardLocation.Board, playerDealOrder++, true, game.CurrentPhase, now);
				dealtCards.Add(new DealtCard { Suit = boardGameCard.Suit, Symbol = boardGameCard.Symbol, DealOrder = boardGameCard.DealOrder });
			}
			else if (game.CurrentPhase == nameof(Phases.SeventhStreet))
			{
				// Deal 1 hole card
				if (deckIndex >= deckCards.Count)
				{
					return new DealHandsError
					{
						Message = "Not enough cards in deck to complete dealing.",
						Code = DealHandsErrorCode.InvalidGameState
					};
				}

				var gameCard = deckCards[deckIndex++];
				AssignCardToPlayer(gameCard, gamePlayer, CardLocation.Hole, playerDealOrder++, false, game.CurrentPhase, now);
				dealtCards.Add(new DealtCard { Suit = gameCard.Suit, Symbol = gameCard.Symbol, DealOrder = gameCard.DealOrder });
			}
			else
			{
				// Deal 1 board card (4th, 5th, 6th streets)
				if (deckIndex >= deckCards.Count)
				{
					return new DealHandsError
					{
						Message = "Not enough cards in deck to complete dealing.",
						Code = DealHandsErrorCode.InvalidGameState
					};
				}

				var gameCard = deckCards[deckIndex++];
				AssignCardToPlayer(gameCard, gamePlayer, CardLocation.Board, playerDealOrder++, true, game.CurrentPhase, now);
				dealtCards.Add(new DealtCard { Suit = gameCard.Suit, Symbol = gameCard.Symbol, DealOrder = gameCard.DealOrder });
			}

			playerHands.Add(new PlayerDealtCards
			{
				PlayerName = gamePlayer.Player.Name,
				SeatPosition = gamePlayer.SeatPosition,
				Cards = dealtCards
			});
		}

		// 6. Reset current bets for all players before betting round
		foreach (var gamePlayer in game.GamePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		// 7. Determine first player to act based on street
		int firstActorSeatPosition;
		int currentBet = 0;

		if (game.CurrentPhase == nameof(Phases.ThirdStreet))
		{
			// Third Street: bring-in player is lowest up card
			//firstActorSeatPosition = FindBringInPlayer(activePlayers, playerHands);
			firstActorSeatPosition = FindBestVisibleHandPlayer(activePlayers, game.Id, game.CurrentHandNumber, context);

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
			// Other streets: player with the best visible hand acts first
			firstActorSeatPosition = FindBestVisibleHandPlayer(activePlayers, game.Id, game.CurrentHandNumber, context);
		}

		// 8. Determine min bet based on street (small bet for 3rd/4th, big bet for 5th/6th/7th)
		var isSmallBetStreet = game.CurrentPhase == nameof(Phases.ThirdStreet) ||
							   game.CurrentPhase == nameof(Phases.FourthStreet);
		var minBet = isSmallBetStreet ? (game.SmallBet ?? game.MinBet ?? 0) : (game.BigBet ?? game.MinBet ?? 0);

		// 9. Create betting round record
		var roundNumber = game.CurrentPhase switch
		{
			nameof(Phases.ThirdStreet) => 1,
			nameof(Phases.FourthStreet) => 2,
			nameof(Phases.FifthStreet) => 3,
			nameof(Phases.SixthStreet) => 4,
			nameof(Phases.SeventhStreet) => 5,
			nameof(Phases.Showdown) => 6,
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

		logger.LogDebug(
			"Creating betting round: Game {GameId}, Hand {HandNumber}, Round {RoundNumber}, Street {Street}, FirstActor {FirstActor}",
			bettingRound.GameId, bettingRound.HandNumber, bettingRound.RoundNumber, bettingRound.Street, firstActorSeatPosition);

		context.BettingRounds.Add(bettingRound);

		// 10. Update game state - remain in street phase for betting
		game.CurrentPlayerIndex = firstActorSeatPosition;
		game.BringInPlayerIndex = game.CurrentPhase == nameof(Phases.ThirdStreet) ? firstActorSeatPosition : -1;
		game.Status = GameStatus.InProgress;
		game.UpdatedAt = now;

		// 11. Persist changes
		await context.SaveChangesAsync(cancellationToken);

		logger.LogInformation(
			"Dealt cards for {Street} and created betting round {RoundNumber} for game {GameId}, hand {HandNumber}",
			game.CurrentPhase, roundNumber, game.Id, game.CurrentHandNumber);

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
	/// Evaluates hand strength including pairs, trips, two pair, and high cards.
	/// </summary>
	private static int FindBestVisibleHandPlayer(List<GamePlayer> activePlayers, Guid gameId, int handNumber, CardsDbContext context)
	{
		int bestSeatPosition = activePlayers.FirstOrDefault()?.SeatPosition ?? 0;
		long bestStrength = -1;

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

			var strength = EvaluateVisibleHand(boardCards);

			if (strength > bestStrength)
			{
				bestStrength = strength;
				bestSeatPosition = player.SeatPosition;
			}
		}

		return bestSeatPosition;
	}

	/// <summary>
	/// Evaluates the strength of visible cards for determining betting order.
	/// Higher values indicate better hands (pairs beat high cards, trips beat pairs, etc.)
	/// </summary>
	private static long EvaluateVisibleHand(List<GameCard> boardCards)
	{
		if (boardCards.Count == 0) return 0;

		var cards = boardCards.OrderByDescending(c => GetCardValue(c.Symbol)).ToList();
		var valueCounts = cards.GroupBy(c => GetCardValue(c.Symbol))
			.OrderByDescending(g => g.Count())
			.ThenByDescending(g => g.Key)
			.ToList();

		long strength = 0;

		// Check for pairs, trips, etc.
		var maxCount = valueCounts.First().Count();

		if (maxCount >= 4)
		{
			// Four of a kind
			strength = 7_000_000 + valueCounts.First().Key * 1000;
		}
		else if (maxCount >= 3)
		{
			// Three of a kind
			strength = 4_000_000 + valueCounts.First().Key * 1000;
		}
		else if (maxCount >= 2)
		{
			var pairs = valueCounts.Where(g => g.Count() >= 2).ToList();
			if (pairs.Count >= 2)
			{
				// Two pair
				strength = 3_000_000 + pairs[0].Key * 1000 + pairs[1].Key * 10;
			}
			else
			{
				// One pair
				strength = 2_000_000 + pairs[0].Key * 1000;
			}
		}
		else
		{
			// High card(s)
			strength = 1_000_000;
		}

		// Add kicker values for tie-breaking
		foreach (var card in cards.Take(4))
		{
			strength = strength * 15 + GetCardValue(card.Symbol);
		}

		// Add suit rank for final tie-breaking (highest suit of the best card wins)
		if (cards.Count > 0)
		{
			strength = strength * 4 + GetSuitRank(cards[0].Suit);
		}

		return strength;
	}

	/// <summary>
	/// Assigns a deck card to a player by updating its properties.
	/// </summary>
	private static void AssignCardToPlayer(
		GameCard gameCard,
		GamePlayer gamePlayer,
		CardLocation location,
		int playerDealOrder,
		bool isVisible,
		string phase,
		DateTimeOffset now)
	{
		gameCard.GamePlayerId = gamePlayer.Id;
		gameCard.Location = location;
		gameCard.DealOrder = playerDealOrder;
		gameCard.DealtAtPhase = phase;
		gameCard.IsVisible = isVisible;
		gameCard.DealtAt = now;
	}
}
