using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Baseball;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using BettingRoundEntity = CardGames.Poker.Api.Data.Entities.BettingRound;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;

public class DealHandsCommandHandler(
	CardsDbContext context,
	ILogger<DealHandsCommandHandler> logger)
	: IRequestHandler<DealHandsCommand, OneOf<DealHandsSuccessful, DealHandsError>>
{
	public async Task<OneOf<DealHandsSuccessful, DealHandsError>> Handle(
		DealHandsCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		logger.LogDebug("DealHandsCommand starting for game {GameId}", command.GameId);

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

		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.HasFolded)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

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
		var buyCardOffers = new List<BaseballGameSettings.BuyCardOfferState>();

		foreach (var gamePlayer in activePlayers)
		{
			var dealtCards = new List<DealtCard>();

			var existingCards = await context.GameCards
				.Where(gc => gc.GamePlayerId == gamePlayer.Id &&
							 gc.HandNumber == game.CurrentHandNumber &&
							 gc.Location != CardLocation.Deck &&
							 !gc.IsDiscarded)
				.Select(gc => new { gc.Symbol, gc.Suit, gc.DealOrder, gc.Location })
				.ToListAsync(cancellationToken);

			var existingCardCount = existingCards.Count;

			if (game.CurrentPhase == nameof(Phases.ThirdStreet) && existingCardCount > 0)
			{
				var staleCards = await context.GameCards
					.Where(gc => gc.GamePlayerId == gamePlayer.Id &&
								 gc.HandNumber == game.CurrentHandNumber &&
								 gc.Location != CardLocation.Deck &&
								 !gc.IsDiscarded)
					.ToListAsync(cancellationToken);

				if (staleCards.Count > 0)
				{
					context.GameCards.RemoveRange(staleCards);
				}

				existingCardCount = 0;
			}

			var playerDealOrder = existingCardCount + 1;

			if (game.CurrentPhase == nameof(Phases.ThirdStreet))
			{
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
					AssignCardToPlayer(gameCard, gamePlayer, CardLocation.Hole, playerDealOrder++, false, game.CurrentPhase, now, isBuyCard: false);
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
				AssignCardToPlayer(boardGameCard, gamePlayer, CardLocation.Board, playerDealOrder++, true, game.CurrentPhase, now, isBuyCard: false);
				dealtCards.Add(new DealtCard { Suit = boardGameCard.Suit, Symbol = boardGameCard.Symbol, DealOrder = boardGameCard.DealOrder });

				if (boardGameCard.Symbol == CardSymbol.Four)
				{
					buyCardOffers.Add(new BaseballGameSettings.BuyCardOfferState(
						gamePlayer.PlayerId,
						gamePlayer.SeatPosition,
						boardGameCard.Id,
						game.CurrentPhase));
				}
			}
			else if (game.CurrentPhase == nameof(Phases.SeventhStreet))
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
				AssignCardToPlayer(gameCard, gamePlayer, CardLocation.Hole, playerDealOrder++, false, game.CurrentPhase, now, isBuyCard: false);
				dealtCards.Add(new DealtCard { Suit = gameCard.Suit, Symbol = gameCard.Symbol, DealOrder = gameCard.DealOrder });
			}
			else
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
				AssignCardToPlayer(gameCard, gamePlayer, CardLocation.Board, playerDealOrder++, true, game.CurrentPhase, now, isBuyCard: false);
				dealtCards.Add(new DealtCard { Suit = gameCard.Suit, Symbol = gameCard.Symbol, DealOrder = gameCard.DealOrder });

				if (gameCard.Symbol == CardSymbol.Four)
				{
					buyCardOffers.Add(new BaseballGameSettings.BuyCardOfferState(
						gamePlayer.PlayerId,
						gamePlayer.SeatPosition,
						gameCard.Id,
						game.CurrentPhase));
				}
			}

			playerHands.Add(new PlayerDealtCards
			{
				PlayerName = gamePlayer.Player.Name,
				SeatPosition = gamePlayer.SeatPosition,
				Cards = dealtCards
			});
		}

		foreach (var gamePlayer in game.GamePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		// Persist dealt cards before determining first actor so visible-hand evaluation
		// includes newly dealt up-cards on this street.
		await context.SaveChangesAsync(cancellationToken);

		int firstActorSeatPosition;
		int currentBet = 0;

		if (game.CurrentPhase == nameof(Phases.ThirdStreet))
		{
			firstActorSeatPosition = FindBringInPlayer(playerHands);

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
			firstActorSeatPosition = FindBestVisibleHandPlayer(activePlayers, game.Id, game.CurrentHandNumber, context);
		}

		var isSmallBetStreet = game.CurrentPhase == nameof(Phases.ThirdStreet) ||
							   game.CurrentPhase == nameof(Phases.FourthStreet);
		var minBet = isSmallBetStreet ? (game.SmallBet ?? game.MinBet ?? 0) : (game.BigBet ?? game.MinBet ?? 0);

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
			MaxRaises = 0,
			LastRaiseAmount = 0,
			PlayersInHand = activePlayers.Count,
			PlayersActed = 0,
			CurrentActorIndex = firstActorSeatPosition,
			LastAggressorIndex = -1,
			IsComplete = false,
			StartedAt = now
		};

		context.BettingRounds.Add(bettingRound);

		var streetPhase = game.CurrentPhase;
		var buyCardState = BaseballGameSettings.GetState(game, game.MinBet ?? 0);

		if (buyCardOffers.Count > 0)
		{
			var updatedState = buyCardState with
			{
				PendingOffers = buyCardOffers,
				ReturnPhase = streetPhase,
				ReturnActorIndex = firstActorSeatPosition
			};
			BaseballGameSettings.SaveState(game, updatedState);
			game.CurrentPhase = nameof(Phases.BuyCardOffer);
			game.CurrentPlayerIndex = buyCardOffers[0].SeatPosition;
		}
		else
		{
			BaseballGameSettings.SaveState(game, buyCardState with
			{
				PendingOffers = [],
				ReturnPhase = null,
				ReturnActorIndex = null
			});
			game.CurrentPhase = streetPhase;
			game.CurrentPlayerIndex = firstActorSeatPosition;
		}

		game.BringInPlayerIndex = streetPhase == nameof(Phases.ThirdStreet) ? firstActorSeatPosition : -1;
		game.Status = GameStatus.InProgress;
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);

		var currentPlayerName = buyCardOffers.Count > 0
			? activePlayers.FirstOrDefault(p => p.SeatPosition == buyCardOffers[0].SeatPosition)?.Player.Name
			: activePlayers.FirstOrDefault(p => p.SeatPosition == firstActorSeatPosition)?.Player.Name;

		return new DealHandsSuccessful
		{
			GameId = game.Id,
			CurrentPhase = game.CurrentPhase,
			HandNumber = game.CurrentHandNumber,
			CurrentPlayerIndex = game.CurrentPlayerIndex,
			CurrentPlayerName = currentPlayerName,
			PlayerHands = playerHands
		};
	}

	private static int FindBringInPlayer(List<PlayerDealtCards> playerHands)
	{
		int lowestSeatPosition = -1;
		DealtCard? lowestCard = null;

		foreach (var playerHand in playerHands)
		{
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

	private static int CompareCardsForBringIn(DealtCard a, DealtCard b)
	{
		var aValue = GetCardValue(a.Symbol);
		var bValue = GetCardValue(b.Symbol);

		if (aValue != bValue)
		{
			return aValue.CompareTo(bValue);
		}

		return GetSuitRank(a.Suit).CompareTo(GetSuitRank(b.Suit));
	}

	private static void AssignCardToPlayer(
		GameCard gameCard,
		GamePlayer gamePlayer,
		CardLocation location,
		int dealOrder,
		bool isVisible,
		string dealtAtPhase,
		DateTimeOffset now,
		bool isBuyCard)
	{
		gameCard.GamePlayerId = gamePlayer.Id;
		gameCard.Location = location;
		gameCard.DealOrder = dealOrder;
		gameCard.IsVisible = isVisible;
		gameCard.DealtAt = now;
		gameCard.DealtAtPhase = dealtAtPhase;
		gameCard.IsBuyCard = isBuyCard;
	}

	private static int FindBestVisibleHandPlayer(
		List<GamePlayer> activePlayers,
		Guid gameId,
		int handNumber,
		CardsDbContext context)
	{
		if (activePlayers.Count == 0)
		{
			return -1;
		}

		var playerUpCards = context.GameCards
			.Where(gc => gc.GameId == gameId &&
						 gc.HandNumber == handNumber &&
						 gc.Location == CardLocation.Board &&
						 gc.GamePlayerId != null &&
						 activePlayers.Select(p => p.Id).Contains(gc.GamePlayerId.Value))
			.ToList();

		var grouped = playerUpCards
			.GroupBy(c => c.GamePlayerId!.Value)
			.ToDictionary(g => g.Key, g => g.ToList());

		int bestSeatPosition = activePlayers.FirstOrDefault()?.SeatPosition ?? 0;
		long bestStrength = -1;

		foreach (var player in activePlayers)
		{
			var boardCards = grouped.TryGetValue(player.Id, out var cards)
				? cards.Where(c => c.IsVisible && !c.IsDiscarded).ToList()
				: [];

			var strength = EvaluateVisibleHand(boardCards);

			if (strength > bestStrength)
			{
				bestStrength = strength;
				bestSeatPosition = player.SeatPosition;
			}
		}

		return bestSeatPosition;
	}

	private static long EvaluateVisibleHand(List<GameCard> boardCards)
	{
		if (boardCards.Count == 0) return 0;

		var cards = boardCards.OrderByDescending(c => GetCardValue(c.Symbol)).ToList();
		var valueCounts = cards.GroupBy(c => GetCardValue(c.Symbol))
			.OrderByDescending(g => g.Count())
			.ThenByDescending(g => g.Key)
			.ToList();

		long strength = 0;
		var maxCount = valueCounts.First().Count();

		if (maxCount >= 4)
		{
			strength = 7_000_000 + valueCounts.First().Key * 1000;
		}
		else if (maxCount >= 3)
		{
			strength = 4_000_000 + valueCounts.First().Key * 1000;
		}
		else if (maxCount >= 2)
		{
			var pairs = valueCounts.Where(g => g.Count() >= 2).ToList();
			if (pairs.Count >= 2)
			{
				strength = 3_000_000 + pairs[0].Key * 1000 + pairs[1].Key * 10;
			}
			else
			{
				strength = 2_000_000 + pairs[0].Key * 1000;
			}
		}
		else
		{
			strength = 1_000_000;
		}

		foreach (var card in cards.Take(4))
		{
			strength = strength * 15 + GetCardValue(card.Symbol);
		}

		if (cards.Count > 0)
		{
			strength = strength * 4 + GetSuitRank(cards[0].Suit);
		}

		return strength;
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
}
