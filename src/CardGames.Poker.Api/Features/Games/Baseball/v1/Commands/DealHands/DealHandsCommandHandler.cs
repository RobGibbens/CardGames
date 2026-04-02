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

using CardGames.Poker.Api.Services.InMemoryEngine;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;

public class DealHandsCommandHandler(
	CardsDbContext context,
	IOptions<InMemoryEngineOptions> engineOptions,
	IGameStateManager gameStateManager,
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

		if (!BaseballGameSettings.IsStreetPhase(game.CurrentPhase))
		{
			logger.LogWarning(
				"Invalid phase for dealing: {Phase}.",
				game.CurrentPhase);

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

		if (activePlayers.Count == 0)
		{
			return new DealHandsError
			{
				Message = "No active players available for dealing.",
				Code = DealHandsErrorCode.InvalidGameState
			};
		}

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

		var streetPhase = game.CurrentPhase;
		var expectedCardsForStreet = BaseballGameSettings.GetExpectedCardsForStreet(streetPhase);
		var deckIndex = 0;
		var playerHands = new List<PlayerDealtCards>();
		var buyCardOffers = new List<BaseballGameSettings.BuyCardOfferState>();

		var existingStreetCards = await context.GameCards
			.Where(gc => gc.HandNumber == game.CurrentHandNumber &&
						 gc.GamePlayerId != null &&
						 gc.Location != CardLocation.Deck &&
						 !gc.IsDiscarded)
			.Select(gc => gc.GamePlayerId!.Value)
			.ToListAsync(cancellationToken);

		var cardCountsByPlayer = existingStreetCards
			.GroupBy(playerId => playerId)
			.ToDictionary(g => g.Key, g => g.Count());

		foreach (var gamePlayer in activePlayers)
		{
			var existingCardCount = cardCountsByPlayer.GetValueOrDefault(gamePlayer.Id, 0);
			if (existingCardCount >= expectedCardsForStreet)
			{
				continue;
			}

			var dealtCards = new List<DealtCard>();
			var playerDealOrder = existingCardCount + 1;

			if (streetPhase == nameof(Phases.ThirdStreet))
			{
				while (playerDealOrder <= 2)
				{
					if (deckIndex >= deckCards.Count)
					{
						return new DealHandsError
						{
							Message = "Not enough cards in deck to complete dealing.",
							Code = DealHandsErrorCode.InvalidGameState
						};
					}

					var holeCard = deckCards[deckIndex++];
					AssignCardToPlayer(holeCard, gamePlayer, CardLocation.Hole, playerDealOrder++, false, streetPhase, now, isBuyCard: false);
					dealtCards.Add(new DealtCard { Suit = holeCard.Suit, Symbol = holeCard.Symbol, DealOrder = holeCard.DealOrder });
					cardCountsByPlayer[gamePlayer.Id] = cardCountsByPlayer.GetValueOrDefault(gamePlayer.Id, 0) + 1;
				}

				if (playerDealOrder == 3)
				{
					if (deckIndex >= deckCards.Count)
					{
						return new DealHandsError
						{
							Message = "Not enough cards in deck to complete dealing.",
							Code = DealHandsErrorCode.InvalidGameState
						};
					}

					var boardCard = deckCards[deckIndex++];
					AssignCardToPlayer(boardCard, gamePlayer, CardLocation.Board, playerDealOrder++, true, streetPhase, now, isBuyCard: false);
					dealtCards.Add(new DealtCard { Suit = boardCard.Suit, Symbol = boardCard.Symbol, DealOrder = boardCard.DealOrder });
					cardCountsByPlayer[gamePlayer.Id] = cardCountsByPlayer.GetValueOrDefault(gamePlayer.Id, 0) + 1;

					if (boardCard.Symbol == CardSymbol.Four)
					{
						buyCardOffers.Add(new BaseballGameSettings.BuyCardOfferState(
							gamePlayer.PlayerId,
							gamePlayer.SeatPosition,
							boardCard.Id,
							streetPhase));
					}
				}
			}
			else if (streetPhase == nameof(Phases.SeventhStreet))
			{
				if (deckIndex >= deckCards.Count)
				{
					return new DealHandsError
					{
						Message = "Not enough cards in deck to complete dealing.",
						Code = DealHandsErrorCode.InvalidGameState
					};
				}

				var downCard = deckCards[deckIndex++];
				AssignCardToPlayer(downCard, gamePlayer, CardLocation.Hole, playerDealOrder++, false, streetPhase, now, isBuyCard: false);
				dealtCards.Add(new DealtCard { Suit = downCard.Suit, Symbol = downCard.Symbol, DealOrder = downCard.DealOrder });
				cardCountsByPlayer[gamePlayer.Id] = cardCountsByPlayer.GetValueOrDefault(gamePlayer.Id, 0) + 1;
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

				var upCard = deckCards[deckIndex++];
				AssignCardToPlayer(upCard, gamePlayer, CardLocation.Board, playerDealOrder++, true, streetPhase, now, isBuyCard: false);
				dealtCards.Add(new DealtCard { Suit = upCard.Suit, Symbol = upCard.Symbol, DealOrder = upCard.DealOrder });
				cardCountsByPlayer[gamePlayer.Id] = cardCountsByPlayer.GetValueOrDefault(gamePlayer.Id, 0) + 1;

				if (upCard.Symbol == CardSymbol.Four)
				{
					buyCardOffers.Add(new BaseballGameSettings.BuyCardOfferState(
						gamePlayer.PlayerId,
						gamePlayer.SeatPosition,
						upCard.Id,
						streetPhase));
				}
			}

			if (dealtCards.Count > 0)
			{
				playerHands.Add(new PlayerDealtCards
				{
					PlayerName = gamePlayer.Player.Name,
					SeatPosition = gamePlayer.SeatPosition,
					Cards = dealtCards
				});
			}

			if (buyCardOffers.Count > 0)
			{
				break;
			}
		}

		var streetComplete = activePlayers.All(p => cardCountsByPlayer.GetValueOrDefault(p.Id, 0) >= expectedCardsForStreet);

		if (buyCardOffers.Count > 0)
		{
			var defaultBuyCardPrice = game.MinBet ?? 0;
			var pendingBuyCardState = BaseballGameSettings.GetState(game, defaultBuyCardPrice);
			BaseballGameSettings.SaveState(game, pendingBuyCardState with
			{
				PendingOffers = [buyCardOffers[0]],
				ReturnPhase = streetPhase,
				ReturnActorIndex = null
			});

			game.CurrentPhase = nameof(Phases.BuyCardOffer);
			game.CurrentPlayerIndex = buyCardOffers[0].SeatPosition;
			game.UpdatedAt = now;

			await context.SaveChangesAsync(cancellationToken);

			if (engineOptions.Value.Enabled)
				await gameStateManager.GetOrLoadGameAsync(command.GameId, cancellationToken);

			var offerPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == buyCardOffers[0].SeatPosition)?.Player.Name;
			return new DealHandsSuccessful
			{
				GameId = game.Id,
				CurrentPhase = game.CurrentPhase,
				HandNumber = game.CurrentHandNumber,
				CurrentPlayerIndex = game.CurrentPlayerIndex,
				CurrentPlayerName = offerPlayerName,
				PlayerHands = playerHands
			};
		}

		if (!streetComplete)
		{
			return new DealHandsError
			{
				Message = "Street dealing was not completed.",
				Code = DealHandsErrorCode.InvalidGameState
			};
		}

		foreach (var gamePlayer in game.GamePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		// Persist dealt cards before determining first actor so visible-hand evaluation
		// includes newly dealt up-cards on this street.
		await context.SaveChangesAsync(cancellationToken);

		if (engineOptions.Value.Enabled)
			await gameStateManager.GetOrLoadGameAsync(command.GameId, cancellationToken);

		int firstActorSeatPosition;
		int currentBet = 0;

		if (streetPhase == nameof(Phases.ThirdStreet))
		{
			firstActorSeatPosition = FindBringInPlayerFromPersistedCards(activePlayers, game.Id, game.CurrentHandNumber, context);

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

		if (firstActorSeatPosition < 0)
		{
			firstActorSeatPosition = GetPlayerLeftOfDealerSeatPosition(activePlayers, game.DealerPosition);
		}

		var isSmallBetStreet = streetPhase == nameof(Phases.ThirdStreet) ||
							   streetPhase == nameof(Phases.FourthStreet);
		var minBet = isSmallBetStreet ? (game.SmallBet ?? game.MinBet ?? 0) : (game.BigBet ?? game.MinBet ?? 0);

		var roundNumber = streetPhase switch
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
			Street = streetPhase,
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

		var buyCardState = BaseballGameSettings.GetState(game, game.MinBet ?? 0);

		BaseballGameSettings.SaveState(game, buyCardState with
		{
			PendingOffers = [],
			ReturnPhase = null,
			ReturnActorIndex = null
		});
		game.CurrentPhase = streetPhase;
		game.CurrentPlayerIndex = firstActorSeatPosition;

		game.BringInPlayerIndex = streetPhase == nameof(Phases.ThirdStreet) ? firstActorSeatPosition : -1;
		game.Status = GameStatus.InProgress;
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);

		if (engineOptions.Value.Enabled)
			await gameStateManager.GetOrLoadGameAsync(command.GameId, cancellationToken);

		var currentPlayerName = activePlayers.FirstOrDefault(p => p.SeatPosition == firstActorSeatPosition)?.Player.Name;

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

	private static int FindBringInPlayerFromPersistedCards(
		List<GamePlayer> activePlayers,
		Guid gameId,
		int handNumber,
		CardsDbContext context)
	{
		var upCards = context.GameCards
			.Where(gc => gc.GameId == gameId &&
						 gc.HandNumber == handNumber &&
						 gc.Location == CardLocation.Board &&
						 gc.DealtAtPhase == nameof(Phases.ThirdStreet) &&
						 !gc.IsBuyCard &&
						 gc.GamePlayerId != null)
			.ToList();

		if (upCards.Count == 0)
		{
			return activePlayers.FirstOrDefault()?.SeatPosition ?? -1;
		}

		var byPlayerId = upCards
			.GroupBy(c => c.GamePlayerId!.Value)
			.ToDictionary(g => g.Key, g => g.First());

		int lowestSeatPosition = -1;
		GameCard? lowestCard = null;

		foreach (var player in activePlayers)
		{
			if (!byPlayerId.TryGetValue(player.Id, out var upCard))
			{
				continue;
			}

			if (lowestCard is null || CompareCardsForBringIn(new DealtCard { Suit = upCard.Suit, Symbol = upCard.Symbol }, new DealtCard { Suit = lowestCard.Suit, Symbol = lowestCard.Symbol }) < 0)
			{
				lowestCard = upCard;
				lowestSeatPosition = player.SeatPosition;
			}
		}

		return lowestSeatPosition >= 0 ? lowestSeatPosition : activePlayers.FirstOrDefault()?.SeatPosition ?? -1;
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

       int bestSeatPosition = -1;
		long bestStrength = -1;

		foreach (var player in activePlayers)
		{
			var boardCards = grouped.TryGetValue(player.Id, out var cards)
				? cards.Where(c => c.IsVisible && !c.IsDiscarded).ToList()
				: [];

         var strength = EvaluateVisibleHand(boardCards, includeWildCards: true);

			if (strength > bestStrength)
			{
				bestStrength = strength;
				bestSeatPosition = player.SeatPosition;
			}
		}

		return bestSeatPosition;
	}

	private static int GetPlayerLeftOfDealerSeatPosition(List<GamePlayer> activePlayers, int dealerSeatPosition)
	{
		if (activePlayers.Count == 0)
		{
			return -1;
		}

		var maxSeatPosition = activePlayers.Max(p => p.SeatPosition);
		var totalSeats = maxSeatPosition + 1;

		return activePlayers
			.OrderBy(p => (p.SeatPosition - dealerSeatPosition - 1 + totalSeats) % totalSeats)
			.Select(p => p.SeatPosition)
			.First();
	}

  private static long EvaluateVisibleHand(List<GameCard> boardCards, bool includeWildCards)
	{
		if (boardCards.Count == 0) return 0;

     var cards = boardCards.OrderByDescending(c => GetCardValue(c.Symbol)).ToList();
		var valueCounts = cards
			.Where(c => !includeWildCards || !IsBaseballWild(c.Symbol))
			.GroupBy(c => GetCardValue(c.Symbol))
			.OrderByDescending(g => g.Count())
			.ThenByDescending(g => g.Key)
          .Select(g => new { Value = g.Key, Count = g.Count() })
			.ToList();

		if (includeWildCards)
		{
			var wildCount = cards.Count(c => IsBaseballWild(c.Symbol));
			if (wildCount > 0)
			{
				if (valueCounts.Count == 0)
				{
					valueCounts.Add(new { Value = 14, Count = wildCount });
				}
				else
				{
					var target = valueCounts
						.OrderByDescending(v => v.Count)
						.ThenByDescending(v => v.Value)
						.First();

					valueCounts.Remove(target);
					valueCounts.Add(new { target.Value, Count = target.Count + wildCount });
					valueCounts = valueCounts
						.OrderByDescending(v => v.Count)
						.ThenByDescending(v => v.Value)
						.ToList();
				}
			}
		}

		long strength = 0;
     var maxCount = valueCounts.First().Count;

		if (maxCount >= 4)
		{
          strength = 7_000_000 + valueCounts.First().Value * 1000;
		}
		else if (maxCount >= 3)
		{
          strength = 4_000_000 + valueCounts.First().Value * 1000;
		}
		else if (maxCount >= 2)
		{
            var pairs = valueCounts.Where(g => g.Count >= 2).ToList();
			if (pairs.Count >= 2)
			{
             strength = 3_000_000 + pairs[0].Value * 1000 + pairs[1].Value * 10;
			}
			else
			{
             strength = 2_000_000 + pairs[0].Value * 1000;
			}
		}
		else
		{
			strength = 1_000_000;
		}

     var kickerValues = valueCounts
			.SelectMany(v => Enumerable.Repeat(v.Value, v.Count))
			.OrderByDescending(v => v)
			.Take(4)
			.ToList();

		if (kickerValues.Count == 0)
		{
			kickerValues = cards.Take(4).Select(c => GetCardValue(c.Symbol)).ToList();
		}

		foreach (var kickerValue in kickerValues)
		{
           strength = strength * 15 + kickerValue;
		}

		if (cards.Count > 0)
		{
			strength = strength * 4 + GetSuitRank(cards[0].Suit);
		}

		return strength;
	}

	private static bool IsBaseballWild(CardSymbol symbol)
	{
		return symbol is CardSymbol.Three or CardSymbol.Nine;
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
