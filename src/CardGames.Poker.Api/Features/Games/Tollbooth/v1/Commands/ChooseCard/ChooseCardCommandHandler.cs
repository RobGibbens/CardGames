using System.Text.Json;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using BettingRound = CardGames.Poker.Api.Data.Entities.BettingRound;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.ChooseCard;

public sealed class ChooseCardCommandHandler(CardsDbContext context)
	: IRequestHandler<ChooseCardCommand, OneOf<ChooseCardSuccessful, ChooseCardError>>
{
	public async Task<OneOf<ChooseCardSuccessful, ChooseCardError>> Handle(
		ChooseCardCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameType)
			.Include(g => g.Pots)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new ChooseCardError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = ChooseCardErrorCode.GameNotFound
			};
		}

		var gameTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code;
		if (!string.Equals(gameTypeCode, PokerGameMetadataRegistry.TollboothCode, StringComparison.OrdinalIgnoreCase))
		{
			return new ChooseCardError
			{
				Message = $"Tollbooth card selection is not supported for game type '{gameTypeCode}'.",
				Code = ChooseCardErrorCode.NotInTollboothPhase
			};
		}

		if (!string.Equals(game.CurrentPhase, nameof(Phases.TollboothOffer), StringComparison.OrdinalIgnoreCase))
		{
			return new ChooseCardError
			{
				Message = $"Cannot choose a card during '{game.CurrentPhase}'. Card selection is only allowed in '{nameof(Phases.TollboothOffer)}'.",
				Code = ChooseCardErrorCode.NotInTollboothPhase
			};
		}

		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		var eligiblePlayers = activePlayers
			.Where(gp => !gp.HasFolded && !gp.HasDrawnThisRound)
			.ToList();

		if (eligiblePlayers.Count == 0)
		{
			return new ChooseCardError
			{
				Message = "No eligible players remain to choose a Tollbooth card.",
				Code = ChooseCardErrorCode.NoEligiblePlayers
			};
		}

		var requestedSeatIndex = command.PlayerSeatIndex ?? game.CurrentDrawPlayerIndex;
		var currentPlayer = activePlayers.FirstOrDefault(gp => gp.SeatPosition == requestedSeatIndex);
		if (currentPlayer is null || currentPlayer.HasFolded)
		{
			return new ChooseCardError
			{
				Message = "The acting player could not be found for Tollbooth card selection.",
				Code = ChooseCardErrorCode.NotPlayerTurn
			};
		}

		if (currentPlayer.HasDrawnThisRound)
		{
			return new ChooseCardError
			{
				Message = "This player has already chosen a card this round.",
				Code = ChooseCardErrorCode.AlreadyChosen
			};
		}

		// Determine cost based on choice
		var ante = game.Ante ?? game.BringIn ?? game.SmallBet ?? game.MinBet ?? 0;
		var cost = command.Choice switch
		{
			TollboothChoice.Furthest => 0,
			TollboothChoice.Nearest => ante,
			TollboothChoice.Deck => ante * 2,
			_ => -1
		};

		if (cost < 0)
		{
			return new ChooseCardError
			{
				Message = "Invalid Tollbooth choice.",
				Code = ChooseCardErrorCode.InvalidChoice
			};
		}

		if (cost > 0 && currentPlayer.ChipStack < cost)
		{
			return new ChooseCardError
			{
				Message = $"Insufficient chips. The {command.Choice} option costs {cost} but player has {currentPlayer.ChipStack}.",
				Code = ChooseCardErrorCode.CannotAfford
			};
		}

		// Load cards for this hand
		var gameCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
						 gc.HandNumber == game.CurrentHandNumber &&
						 !gc.IsDiscarded)
			.ToListAsync(cancellationToken);

		var communityCards = gameCards
			.Where(gc => gc.Location == CardLocation.Community && gc.GamePlayerId == null)
			.OrderBy(gc => gc.DealOrder)
			.ToList();

		var deckCards = gameCards
			.Where(gc => gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToList();

		// Pick the chosen card
		GameCard chosenCard;
		bool replenishDisplay;

		switch (command.Choice)
		{
			case TollboothChoice.Furthest:
				if (communityCards.Count == 0)
				{
					return new ChooseCardError
					{
						Message = "No display cards available.",
						Code = ChooseCardErrorCode.InvalidChoice
					};
				}
				chosenCard = communityCards[^1]; // highest DealOrder = furthest from deck
				replenishDisplay = true;
				break;

			case TollboothChoice.Nearest:
				if (communityCards.Count == 0)
				{
					return new ChooseCardError
					{
						Message = "No display cards available.",
						Code = ChooseCardErrorCode.InvalidChoice
					};
				}
				chosenCard = communityCards[0]; // lowest DealOrder = nearest to deck
				replenishDisplay = true;
				break;

			case TollboothChoice.Deck:
				if (deckCards.Count == 0)
				{
					return new ChooseCardError
					{
						Message = "No deck cards available.",
						Code = ChooseCardErrorCode.InvalidChoice
					};
				}
				chosenCard = deckCards[0]; // top of deck
				replenishDisplay = false;
				break;

			default:
				return new ChooseCardError
				{
					Message = "Invalid Tollbooth choice.",
					Code = ChooseCardErrorCode.InvalidChoice
				};
		}

		// Determine card placement: 7th street card → Hole (face down), otherwise → Board (face up)
		var previousStreet = TollboothVariantState.GetPreviousBettingStreet(game);
		var isSeventhStreetCard = previousStreet == nameof(Phases.SixthStreet);
		var cardLocation = isSeventhStreetCard ? CardLocation.Hole : CardLocation.Board;
		var isVisible = !isSeventhStreetCard;

		var nextStreet = GetNextBettingStreet(previousStreet);

		// Count existing non-deck cards for this player to determine DealOrder
		var existingCardCount = gameCards.Count(gc =>
			gc.GamePlayerId == currentPlayer.Id && gc.Location != CardLocation.Deck);
		var playerDealOrder = existingCardCount + 1;

		// Assign card to player
		chosenCard.GamePlayerId = currentPlayer.Id;
		chosenCard.Location = cardLocation;
		chosenCard.DealOrder = playerDealOrder;
		chosenCard.IsVisible = isVisible;
		chosenCard.DealtAt = now;
		chosenCard.DealtAtPhase = nextStreet;

		// Replenish display from deck if the card came from community
		if (replenishDisplay && deckCards.Count > 0)
		{
			var replenishCard = deckCards.FirstOrDefault(dc => dc != chosenCard);
			if (replenishCard is not null)
			{
				var maxCommunityDealOrder = communityCards
					.Where(c => c != chosenCard)
					.Select(c => c.DealOrder)
					.DefaultIfEmpty(0)
					.Max();

				replenishCard.GamePlayerId = null;
				replenishCard.Location = CardLocation.Community;
				replenishCard.DealOrder = maxCommunityDealOrder + 1;
				replenishCard.IsVisible = true;
				replenishCard.DealtAt = now;
				replenishCard.DealtAtPhase = nameof(Phases.TollboothOffer);
			}
		}

		// Charge the player
		if (cost > 0)
		{
			currentPlayer.ChipStack -= cost;
			currentPlayer.TotalContributedThisHand += cost;

			var mainPot = game.Pots.FirstOrDefault(p =>
				p.PotOrder == 0 && p.HandNumber == game.CurrentHandNumber);
			if (mainPot is not null)
			{
				mainPot.Amount += cost;
			}

			if (currentPlayer.ChipStack == 0)
			{
				currentPlayer.IsAllIn = true;
			}
		}

		currentPlayer.HasDrawnThisRound = true;

		// Find next pending player
		var nextPlayer = activePlayers
			.Where(gp => !gp.HasFolded && !gp.HasDrawnThisRound)
			.OrderBy(gp => gp.SeatPosition)
			.FirstOrDefault();

		var offerComplete = nextPlayer is null;
		var nextSeatIndex = -1;
		string? nextPlayerName = null;

		if (offerComplete)
		{
			// Reset HasDrawnThisRound for next TollboothOffer round
			foreach (var p in activePlayers.Where(gp => !gp.HasFolded))
			{
				p.HasDrawnThisRound = false;
			}

			// Transition to next betting street
			TollboothVariantState.SetPreviousBettingStreet(game, nextStreet);
			game.CurrentDrawPlayerIndex = -1;

			// Reset current bets
			foreach (var p in game.GamePlayers)
			{
				p.CurrentBet = 0;
			}

			var playersStillInHand = activePlayers
				.Where(p => !p.HasFolded)
				.ToList();
			var playersWhoCanBet = playersStillInHand
				.Where(p => !p.IsAllIn && p.ChipStack > 0)
				.ToList();

			if (playersStillInHand.Count <= 1 || playersWhoCanBet.Count < 2)
			{
				// Deal remaining street cards when all players are all-in on pre-SeventhStreet
				if (playersWhoCanBet.Count < 2 && nextStreet != nameof(Phases.SeventhStreet))
				{
					var streetOrder = new[]
					{
						nameof(Phases.FourthStreet),
						nameof(Phases.FifthStreet),
						nameof(Phases.SixthStreet),
						nameof(Phases.SeventhStreet)
					};

					// nextStreet was already dealt via this offer round — deal streets AFTER it
					var startIndex = Array.IndexOf(streetOrder, nextStreet) + 1;

					var remainingDeckCards = gameCards
						.Where(gc => gc.Location == CardLocation.Deck)
						.OrderBy(gc => gc.DealOrder)
						.ToList();

					var deckIdx = 0;
					var dealtStreets = new List<string>();

					for (var streetIdx = startIndex; streetIdx < streetOrder.Length; streetIdx++)
					{
						var street = streetOrder[streetIdx];
						dealtStreets.Add(street);

						foreach (var player in playersStillInHand.OrderBy(p => p.SeatPosition))
						{
							if (deckIdx >= remainingDeckCards.Count)
								break;

							var existingCards = gameCards.Count(gc =>
								gc.GamePlayerId == player.Id && gc.Location != CardLocation.Deck);

							var card = remainingDeckCards[deckIdx++];
							card.GamePlayerId = player.Id;
							card.Location = street == nameof(Phases.SeventhStreet) ? CardLocation.Hole : CardLocation.Board;
							card.DealOrder = existingCards + 1;
							card.IsVisible = street != nameof(Phases.SeventhStreet);
							card.DealtAt = now;
							card.DealtAtPhase = street;
						}
					}

					// Store runout information for client-side animation
					if (dealtStreets.Count > 0)
					{
						var existingSettings = string.IsNullOrEmpty(game.GameSettings)
							? new Dictionary<string, object>()
							: JsonSerializer.Deserialize<Dictionary<string, object>>(game.GameSettings) ?? new Dictionary<string, object>();

						existingSettings["allInRunout"] = true;
						existingSettings["runoutStreets"] = dealtStreets;
						existingSettings["runoutHandNumber"] = game.CurrentHandNumber;
						existingSettings["runoutTimestamp"] = now.ToString("O");

						game.GameSettings = JsonSerializer.Serialize(existingSettings);
					}
				}

				game.CurrentPhase = nameof(Phases.Showdown);
				game.CurrentPlayerIndex = -1;
			}
			else
			{
				game.CurrentPhase = nextStreet;

				// Determine first actor (best visible hand for 4th+ street)
				var firstActorSeat = FindBestVisibleHandPlayer(playersStillInHand, game.Id, game.CurrentHandNumber);
				if (firstActorSeat < 0)
				{
					firstActorSeat = GetPlayerLeftOfDealer(activePlayers, game.DealerPosition);
				}

				// Determine bet sizing
				var isSmallBetStreet = nextStreet == nameof(Phases.FourthStreet);
				var minBet = isSmallBetStreet
					? (game.SmallBet ?? game.MinBet ?? 0)
					: (game.BigBet ?? game.MinBet ?? 0);

				var roundNumber = nextStreet switch
				{
					nameof(Phases.FourthStreet) => 2,
					nameof(Phases.FifthStreet) => 3,
					nameof(Phases.SixthStreet) => 4,
					nameof(Phases.SeventhStreet) => 5,
					_ => 2
				};

				var bettingRound = new BettingRound
				{
					GameId = game.Id,
					HandNumber = game.CurrentHandNumber,
					RoundNumber = roundNumber,
					Street = nextStreet,
					CurrentBet = 0,
					MinBet = minBet,
					RaiseCount = 0,
					MaxRaises = 0,
					LastRaiseAmount = 0,
					PlayersInHand = playersStillInHand.Count,
					PlayersActed = 0,
					CurrentActorIndex = firstActorSeat,
					LastAggressorIndex = -1,
					IsComplete = false,
					StartedAt = now
				};

				context.BettingRounds.Add(bettingRound);
				game.CurrentPlayerIndex = firstActorSeat;
			}
		}
		else
		{
			game.CurrentDrawPlayerIndex = nextPlayer!.SeatPosition;
			game.CurrentPlayerIndex = nextPlayer.SeatPosition;
			nextSeatIndex = nextPlayer.SeatPosition;
			nextPlayerName = nextPlayer.Player.Name;
		}

		game.UpdatedAt = now;
		await context.SaveChangesAsync(cancellationToken);

		return new ChooseCardSuccessful
		{
			GameId = game.Id,
			PlayerName = currentPlayer.Player.Name,
			PlayerSeatIndex = currentPlayer.SeatPosition,
			Choice = command.Choice,
			Cost = cost,
			OfferRoundComplete = offerComplete,
			CurrentPhase = game.CurrentPhase,
			NextPlayerSeatIndex = nextSeatIndex,
			NextPlayerName = nextPlayerName
		};
	}

	private static string GetNextBettingStreet(string? previousStreet)
	{
		return previousStreet switch
		{
			nameof(Phases.ThirdStreet) => nameof(Phases.FourthStreet),
			nameof(Phases.FourthStreet) => nameof(Phases.FifthStreet),
			nameof(Phases.FifthStreet) => nameof(Phases.SixthStreet),
			nameof(Phases.SixthStreet) => nameof(Phases.SeventhStreet),
			_ => nameof(Phases.FourthStreet)
		};
	}

	private int FindBestVisibleHandPlayer(List<GamePlayer> activePlayers, Guid gameId, int handNumber)
	{
		var bestSeatPosition = -1;
		long bestStrength = -1;

		foreach (var player in activePlayers)
		{
			var boardCards = context.GameCards
				.Where(gc => gc.GamePlayerId == player.Id &&
							 gc.HandNumber == handNumber &&
							 gc.Location == CardLocation.Board &&
							 gc.IsVisible && !gc.IsDiscarded)
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

	private static long EvaluateVisibleHand(List<GameCard> boardCards)
	{
		if (boardCards.Count == 0) return 0;

		var cards = boardCards.OrderByDescending(c => GetCardValue(c.Symbol)).ToList();
		var valueCounts = cards.GroupBy(c => GetCardValue(c.Symbol))
			.OrderByDescending(g => g.Count())
			.ThenByDescending(g => g.Key)
			.ToList();

		long strength = 0;
		var maxCount = valueCounts[0].Count();

		if (maxCount >= 4)
			strength = 7_000_000 + valueCounts[0].Key * 1000;
		else if (maxCount >= 3)
			strength = 4_000_000 + valueCounts[0].Key * 1000;
		else if (maxCount >= 2)
		{
			var pairs = valueCounts.Where(g => g.Count() >= 2).ToList();
			strength = pairs.Count >= 2
				? 3_000_000 + pairs[0].Key * 1000 + pairs[1].Key * 10
				: 2_000_000 + pairs[0].Key * 1000;
		}
		else
		{
			strength = 1_000_000;
		}

		foreach (var card in cards.Take(4))
			strength = strength * 15 + GetCardValue(card.Symbol);

		if (cards.Count > 0)
			strength = strength * 4 + GetSuitRank(cards[0].Suit);

		return strength;
	}

	private static int GetCardValue(CardSymbol symbol) => symbol switch
	{
		CardSymbol.Deuce => 2, CardSymbol.Three => 3,
		CardSymbol.Four => 4, CardSymbol.Five => 5,
		CardSymbol.Six => 6, CardSymbol.Seven => 7,
		CardSymbol.Eight => 8, CardSymbol.Nine => 9,
		CardSymbol.Ten => 10, CardSymbol.Jack => 11,
		CardSymbol.Queen => 12, CardSymbol.King => 13,
		CardSymbol.Ace => 14, _ => 0
	};

	private static int GetSuitRank(CardSuit suit) => suit switch
	{
		CardSuit.Clubs => 0, CardSuit.Diamonds => 1,
		CardSuit.Hearts => 2, CardSuit.Spades => 3, _ => 0
	};

	private static int GetPlayerLeftOfDealer(List<GamePlayer> activePlayers, int dealerSeatPosition)
	{
		if (activePlayers.Count == 0) return -1;

		var maxSeatPosition = activePlayers.Max(p => p.SeatPosition);
		var totalSeats = maxSeatPosition + 1;

		return activePlayers
			.Where(p => !p.HasFolded && !p.IsAllIn)
			.OrderBy(p => (p.SeatPosition - dealerSeatPosition - 1 + totalSeats) % totalSeats)
			.Select(p => p.SeatPosition)
			.FirstOrDefault(-1);
	}
}
