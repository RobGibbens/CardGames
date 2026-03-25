using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Tollbooth;
using CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.ChooseCard;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.Tollbooth;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Tollbooth poker.
/// Deals Third Street like standard Seven Card Stud, then uses TollboothOffer phases
/// for Fourth through Seventh streets where each player picks a card from display or deck.
/// </summary>
public sealed class TollboothFlowHandler : BaseGameFlowHandler
{
	public override string GameTypeCode => PokerGameMetadataRegistry.TollboothCode;

	public override GameRules GetGameRules() => TollboothRules.CreateGameRules();

	/// <summary>
	/// TollboothOffer is a special phase that does not set up a betting round.
	/// </summary>
	public override IReadOnlyList<string> SpecialPhases => [nameof(Phases.TollboothOffer)];

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
				// Fourth through Seventh street cards are dealt via TollboothOffer,
				// not via the standard dealing pipeline
				new DealingRoundConfig
				{
					PhaseName = nameof(Phases.FourthStreet),
					HoleCards = 0,
					BoardCards = 0,
					HasBettingAfter = true
				},
				new DealingRoundConfig
				{
					PhaseName = nameof(Phases.FifthStreet),
					HoleCards = 0,
					BoardCards = 0,
					HasBettingAfter = true
				},
				new DealingRoundConfig
				{
					PhaseName = nameof(Phases.SixthStreet),
					HoleCards = 0,
					BoardCards = 0,
					HasBettingAfter = true
				},
				new DealingRoundConfig
				{
					PhaseName = nameof(Phases.SeventhStreet),
					HoleCards = 0,
					BoardCards = 0,
					HasBettingAfter = true
				}
			]
		};
	}

	/// <summary>
	/// Tollbooth phase transitions:
	/// ThirdStreet betting completes → TollboothOffer (for 4th street cards)
	/// FourthStreet betting completes → TollboothOffer (for 5th street cards)
	/// FifthStreet betting completes → TollboothOffer (for 6th street cards)
	/// SixthStreet betting completes → TollboothOffer (for 7th street cards)
	/// SeventhStreet betting completes → Showdown
	///
	/// TollboothOffer → next betting street (managed by the ChooseCard command handler)
	/// </summary>
	public override string? GetNextPhase(Game game, string currentPhase)
	{
		if (IsSinglePlayerRemaining(game) && !IsResolutionPhase(currentPhase))
		{
			return nameof(Phases.Showdown);
		}

		return currentPhase switch
		{
			nameof(Phases.CollectingAntes) => nameof(Phases.ThirdStreet),
			nameof(Phases.ThirdStreet) => nameof(Phases.TollboothOffer),
			nameof(Phases.FourthStreet) => nameof(Phases.TollboothOffer),
			nameof(Phases.FifthStreet) => nameof(Phases.TollboothOffer),
			nameof(Phases.SixthStreet) => nameof(Phases.TollboothOffer),
			nameof(Phases.SeventhStreet) => nameof(Phases.Showdown),
			nameof(Phases.TollboothOffer) => GetNextBettingStreetAfterOffer(game),
			nameof(Phases.Showdown) => nameof(Phases.Complete),
			_ => base.GetNextPhase(game, currentPhase)
		};
	}

	/// <summary>
	/// Determines which betting street follows the current TollboothOffer round
	/// based on the previous betting street stored in the game's variant state.
	/// </summary>
	private static string GetNextBettingStreetAfterOffer(Game game)
	{
		var previousStreet = TollboothVariantState.GetPreviousBettingStreet(game);
		return previousStreet switch
		{
			nameof(Phases.ThirdStreet) => nameof(Phases.FourthStreet),
			nameof(Phases.FourthStreet) => nameof(Phases.FifthStreet),
			nameof(Phases.FifthStreet) => nameof(Phases.SixthStreet),
			nameof(Phases.SixthStreet) => nameof(Phases.SeventhStreet),
			_ => nameof(Phases.FourthStreet) // fallback: first offer after Third Street
		};
	}

	/// <summary>
	/// Deals Third Street cards (2 hole + 1 board per player) and sets up two initial
	/// Tollbooth display cards as Community cards for UI rendering.
	/// </summary>
	public override async Task DealCardsAsync(
		CardsDbContext context,
		Game game,
		List<GamePlayer> eligiblePlayers,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id &&
						 gc.HandNumber == game.CurrentHandNumber &&
						 gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync(cancellationToken);

		if (deckCards.Count == 0)
		{
			var deck = CreateShuffledDeck();
			var generatedDeckCards = new List<GameCard>();
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
					DealtAt = now
				};
				generatedDeckCards.Add(gameCard);
				context.GameCards.Add(gameCard);
			}

			deckCards = generatedDeckCards;
		}

		var deckIndex = 0;
		var dealerPosition = game.DealerPosition;
		var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
		var totalSeats = maxSeatPosition + 1;

		var playersInDealOrder = eligiblePlayers
			.OrderBy(p => (p.SeatPosition - dealerPosition - 1 + totalSeats) % totalSeats)
			.ToList();

		var playerUpCards = new List<(GamePlayer Player, GameCard UpCard)>();
		var dealOrder = 1;

		// Deal Third Street: 2 hole cards + 1 board card per player
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
		}

		// Place two initial Tollbooth display cards as Community cards (visible, no player)
		for (var i = 0; i < 2; i++)
		{
			if (deckIndex >= deckCards.Count) break;

			var displayCard = deckCards[deckIndex++];
			displayCard.GamePlayerId = null;
			displayCard.Location = CardLocation.Community;
			displayCard.DealOrder = dealOrder++;
			displayCard.IsVisible = true;
			displayCard.DealtAtPhase = nameof(Phases.ThirdStreet);
			displayCard.DealtAt = now;
		}

		foreach (var player in game.GamePlayers)
		{
			player.CurrentBet = 0;
		}

		// Determine bring-in player
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

		game.CurrentPhase = nameof(Phases.ThirdStreet);
		game.CurrentPlayerIndex = bringInSeatPosition;
		game.BringInPlayerIndex = bringInSeatPosition;
		game.UpdatedAt = now;

		// Initialize variant state to track previous betting street
		TollboothVariantState.SetPreviousBettingStreet(game, nameof(Phases.ThirdStreet));

		await context.SaveChangesAsync(cancellationToken);
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

	/// <inheritdoc />
	protected override async Task SendBettingActionAsync(AutoActionContext context, Data.Entities.BettingActionType action, int amount = 0)
	{
		var command = new Features.Games.Tollbooth.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand(
			context.GameId, action, amount);

		try
		{
			await context.Mediator.Send(command, context.CancellationToken);
		}
		catch (Exception ex)
		{
			context.Logger.LogError(ex, "Error performing auto-betting action for Tollbooth game {GameId}", context.GameId);
		}
	}

	/// <inheritdoc />
	public override async Task PerformAutoActionAsync(AutoActionContext context)
	{
		if (context.CurrentPhase.Equals(nameof(Phases.TollboothOffer), StringComparison.OrdinalIgnoreCase))
		{
			await PerformAutoTollboothChoiceAsync(context);
		}
		else
		{
			await base.PerformAutoActionAsync(context);
		}
	}

	private static async Task PerformAutoTollboothChoiceAsync(AutoActionContext context)
	{
		context.Logger.LogInformation(
			"Performing auto-tollbooth choice (Furthest/free) for game {GameId}, seat {SeatIndex}",
			context.GameId, context.PlayerSeatIndex);

		try
		{
			var command = new ChooseCardCommand(
				context.GameId,
				TollboothChoice.Furthest,
				context.PlayerSeatIndex);

			await context.Mediator.Send(command, context.CancellationToken);

			context.Logger.LogInformation(
				"Auto-tollbooth choice completed for game {GameId}, seat {SeatIndex}",
				context.GameId, context.PlayerSeatIndex);
		}
		catch (Exception ex)
		{
			context.Logger.LogError(ex,
				"Auto-tollbooth choice failed for game {GameId}, seat {SeatIndex}",
				context.GameId, context.PlayerSeatIndex);
		}
	}
}
