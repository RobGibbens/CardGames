using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Baseball;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.Baseball;
using CardGames.Poker.Games.GameFlow;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Baseball poker.
/// </summary>
public sealed class BaseballFlowHandler : BaseGameFlowHandler
{
	/// <inheritdoc />
	public override string GameTypeCode => "BASEBALL";

	/// <inheritdoc />
	public override GameRules GetGameRules() => new BaseballGame().GetGameRules();

	/// <inheritdoc />
	public override IReadOnlyList<string> SpecialPhases => [nameof(Phases.BuyCardOffer)];

	/// <inheritdoc />
	public override DealingConfiguration GetDealingConfiguration()
	{
		return new DealingConfiguration
		{
			PatternType = DealingPatternType.StreetBased,
			DealingRounds =
			[
				new DealingRoundConfig
				{
					PhaseName = nameof(Phases.ThirdStreet),
					HoleCards = 2,
					BoardCards = 1,
					HasBettingAfter = true
				},
				new DealingRoundConfig
				{
					PhaseName = nameof(Phases.FourthStreet),
					HoleCards = 0,
					BoardCards = 1,
					HasBettingAfter = true
				},
				new DealingRoundConfig
				{
					PhaseName = nameof(Phases.FifthStreet),
					HoleCards = 0,
					BoardCards = 1,
					HasBettingAfter = true
				},
				new DealingRoundConfig
				{
					PhaseName = nameof(Phases.SixthStreet),
					HoleCards = 0,
					BoardCards = 1,
					HasBettingAfter = true
				},
				new DealingRoundConfig
				{
					PhaseName = nameof(Phases.SeventhStreet),
					HoleCards = 1,
					BoardCards = 0,
					HasBettingAfter = true
				}
			]
		};
	}

	/// <inheritdoc />
	public override string? GetNextPhase(Game game, string currentPhase)
	{
		if (IsSinglePlayerRemaining(game) && !IsResolutionPhase(currentPhase))
		{
			return nameof(Phases.Showdown);
		}

		if (string.Equals(currentPhase, nameof(Phases.BuyCardOffer), StringComparison.OrdinalIgnoreCase))
		{
			var buyCardState = BaseballGameSettings.GetState(game, game.MinBet ?? 0);
			return buyCardState.ReturnPhase ?? nameof(Phases.ThirdStreet);
		}

		return currentPhase switch
		{
			nameof(Phases.CollectingAntes) => nameof(Phases.ThirdStreet),
			nameof(Phases.ThirdStreet) => nameof(Phases.FourthStreet),
			nameof(Phases.FourthStreet) => nameof(Phases.FifthStreet),
			nameof(Phases.FifthStreet) => nameof(Phases.SixthStreet),
			nameof(Phases.SixthStreet) => nameof(Phases.SeventhStreet),
			nameof(Phases.SeventhStreet) => nameof(Phases.Showdown),
			nameof(Phases.Showdown) => nameof(Phases.Complete),
			_ => base.GetNextPhase(game, currentPhase)
		};
	}

	/// <inheritdoc />
	public override async Task DealCardsAsync(
		CardsDbContext context,
		Game game,
		List<GamePlayer> eligiblePlayers,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var deck = CreateShuffledDeck();
		var deckCards = new List<GameCard>();
		var deckOrder = 0;

		foreach (var (suit, symbol) in deck)
		{
			var gameCard = new GameCard
			{
				GameId = game.Id,
				GamePlayerId = null,
				HandNumber = game.CurrentHandNumber,
				Suit = suit,
				Symbol = symbol,
				DealOrder = deckOrder++,
				Location = CardLocation.Deck,
				IsVisible = false,
				IsDiscarded = false,
				IsBuyCard = false,
				DealtAt = now
			};
			deckCards.Add(gameCard);
			context.GameCards.Add(gameCard);
		}

		var deckIndex = 0;

		var dealerPosition = game.DealerPosition;
		var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
		var totalSeats = maxSeatPosition + 1;

		var playersInDealOrder = eligiblePlayers
			.OrderBy(p => (p.SeatPosition - dealerPosition - 1 + totalSeats) % totalSeats)
			.ToList();

		var playerUpCards = new List<(GamePlayer Player, GameCard UpCard)>();
		var buyCardOffers = new List<BaseballGameSettings.BuyCardOfferState>();

		var dealOrder = 1;
		foreach (var player in playersInDealOrder)
		{
			for (var i = 0; i < 2; i++)
			{
				if (deckIndex >= deckCards.Count) break;

				var card = deckCards[deckIndex++];
				card.GamePlayerId = player.Id;
				card.Location = CardLocation.Hole;
				card.DealOrder = dealOrder++;
				card.IsVisible = false;
				card.DealtAtPhase = nameof(Phases.ThirdStreet);
				card.DealtAt = now;
			}

			if (deckIndex >= deckCards.Count) break;

			var boardCard = deckCards[deckIndex++];
			boardCard.GamePlayerId = player.Id;
			boardCard.Location = CardLocation.Board;
			boardCard.DealOrder = dealOrder++;
			boardCard.IsVisible = true;
			boardCard.DealtAtPhase = nameof(Phases.ThirdStreet);
			boardCard.DealtAt = now;

			playerUpCards.Add((player, boardCard));

			if (boardCard.Symbol == CardSymbol.Four)
			{
				buyCardOffers.Add(new BaseballGameSettings.BuyCardOfferState(
					player.PlayerId,
					player.SeatPosition,
					boardCard.Id,
					nameof(Phases.ThirdStreet)));
			}
		}

		foreach (var player in game.GamePlayers)
		{
			player.CurrentBet = 0;
		}

		var bringInPlayer = FindBringInPlayer(playerUpCards);
		var bringInSeatPosition = bringInPlayer?.SeatPosition ??
			playersInDealOrder.FirstOrDefault()?.SeatPosition ?? 0;

		var bringIn = game.BringIn ?? 0;
		var currentBet = 0;
		if (bringIn > 0 && bringInPlayer is not null)
		{
			var actualBringIn = Math.Min(bringIn, bringInPlayer.ChipStack);
			bringInPlayer.CurrentBet = actualBringIn;
			bringInPlayer.ChipStack -= actualBringIn;
			bringInPlayer.TotalContributedThisHand += actualBringIn;
			currentBet = actualBringIn;

			var pot = await context.Pots
				.FirstOrDefaultAsync(p => p.GameId == game.Id &&
										  p.HandNumber == game.CurrentHandNumber &&
										  p.PotType == PotType.Main,
					cancellationToken);
			if (pot is not null)
			{
				pot.Amount += actualBringIn;
			}
		}

		var minBet = game.SmallBet ?? game.MinBet ?? 0;
		var bettingRound = new Data.Entities.BettingRound
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			RoundNumber = 1,
			Street = nameof(Phases.ThirdStreet),
			CurrentBet = currentBet,
			MinBet = minBet,
			RaiseCount = 0,
			MaxRaises = 0,
			LastRaiseAmount = 0,
			PlayersInHand = eligiblePlayers.Count,
			PlayersActed = 0,
			CurrentActorIndex = bringInSeatPosition,
			LastAggressorIndex = -1,
			IsComplete = false,
			StartedAt = now
		};

		context.Set<Data.Entities.BettingRound>().Add(bettingRound);

		var defaultBuyCardPrice = game.MinBet ?? 0;
		var buyCardState = BaseballGameSettings.GetState(game, defaultBuyCardPrice);
		var resolvedBuyCardPrice = buyCardState.BuyCardPrice > 0 ? buyCardState.BuyCardPrice : defaultBuyCardPrice;

		if (buyCardOffers.Count > 0)
		{
			var updatedState = buyCardState with
			{
				BuyCardPrice = resolvedBuyCardPrice,
				PendingOffers = buyCardOffers,
				ReturnPhase = nameof(Phases.ThirdStreet),
				ReturnActorIndex = bringInSeatPosition
			};

			BaseballGameSettings.SaveState(game, updatedState);
			game.CurrentPhase = nameof(Phases.BuyCardOffer);
			game.CurrentPlayerIndex = buyCardOffers[0].SeatPosition;
		}
		else
		{
			BaseballGameSettings.SaveState(game, buyCardState with
			{
				BuyCardPrice = resolvedBuyCardPrice,
				PendingOffers = [],
				ReturnPhase = null,
				ReturnActorIndex = null
			});

			game.CurrentPhase = nameof(Phases.ThirdStreet);
			game.CurrentPlayerIndex = bringInSeatPosition;
		}

		game.BringInPlayerIndex = bringInSeatPosition;
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);
	}

	/// <inheritdoc />
	public override async Task PerformAutoActionAsync(AutoActionContext context)
	{
		if (string.Equals(context.CurrentPhase, nameof(Phases.BuyCardOffer), StringComparison.OrdinalIgnoreCase) &&
			context.PlayerSeatIndex >= 0)
		{
			var player = context.Game.GamePlayers.FirstOrDefault(gp => gp.SeatPosition == context.PlayerSeatIndex);
			if (player is null)
			{
				return;
			}

			await context.Mediator.Send(
				new ProcessBuyCardCommand(context.GameId, player.PlayerId, Accept: false),
				context.CancellationToken);
			return;
		}

		await base.PerformAutoActionAsync(context);
	}

	private static GamePlayer? FindBringInPlayer(List<(GamePlayer Player, GameCard UpCard)> playerUpCards)
	{
		if (playerUpCards.Count == 0)
		{
			return null;
		}

		GamePlayer? lowestPlayer = null;
		GameCard? lowestCard = null;

		foreach (var (player, upCard) in playerUpCards)
		{
			if (lowestCard is null || CompareCardsForBringIn(upCard, lowestCard) < 0)
			{
				lowestCard = upCard;
				lowestPlayer = player;
			}
		}

		return lowestPlayer;
	}

	private static int CompareCardsForBringIn(GameCard a, GameCard b)
	{
		var aValue = GetCardValue(a.Symbol);
		var bValue = GetCardValue(b.Symbol);

		if (aValue != bValue)
		{
			return aValue.CompareTo(bValue);
		}

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
}
