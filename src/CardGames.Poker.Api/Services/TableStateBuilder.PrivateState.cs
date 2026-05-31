using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using CardGames.Contracts.SignalR;
using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.BobBarker;
using CardGames.Poker.Api.Features.Profile;
using CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetHandHistory;
using CardGames.Poker.Api.Features.Games.Baseball;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;
using Microsoft.EntityFrameworkCore;
using Entities = CardGames.Poker.Api.Data.Entities;
using static CardGames.Poker.Api.Services.TableVariantClassifier;
using static CardGames.Poker.Api.Services.TableCardMapper;
using static CardGames.Poker.Api.Services.TableHandEvaluators;


namespace CardGames.Poker.Api.Services;

public sealed partial class TableStateBuilder
{

	/// <inheritdoc />
	public async Task<PrivateStateDto?> BuildPrivateStateAsync(Guid gameId, string userId, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("BuildPrivateStateAsync called for game {GameId}, userId {UserId}", gameId, userId);

		var game = await _context.Games
			.Include(g => g.GameType)
			.AsNoTracking()
			.FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

		if (game is null)
		{
			_logger.LogWarning("BuildPrivateStateAsync: Game {GameId} not found", gameId);
			return null;
		}

		// Find the player by matching the authenticated user id.
		// SignalR `Clients.User(userId)` now routes by email claim, so prefer email/name matching.
		var gamePlayer = await _context.GamePlayers
			.Where(gp => gp.GameId == gameId && gp.Status != Entities.GamePlayerStatus.Left)
			.Include(gp => gp.Player)
			.Include(gp => gp.Cards)
			.AsNoTracking()
			.FirstOrDefaultAsync(gp =>
				gp.Player.Email == userId ||
				gp.Player.Name == userId ||
				gp.Player.ExternalId == userId, cancellationToken);

		if (gamePlayer is null)
		{
			// User is not a player in this game
			_logger.LogWarning(
				"BuildPrivateStateAsync: No player found for userId {UserId} in game {GameId}. " +
				"Checking all players in game...", userId, gameId);

			// Log all players for debugging
			var allPlayers = await _context.GamePlayers
				.Where(gp => gp.GameId == gameId)
				.Include(gp => gp.Player)
				.AsNoTracking()
				.Select(gp => new { gp.Player.Email, gp.Player.Name, gp.Player.ExternalId })
				.ToListAsync(cancellationToken);

			foreach (var p in allPlayers)
			{
				_logger.LogWarning(
					"  Player in game: Email={Email}, Name={Name}, ExternalId={ExternalId}",
					p.Email ?? "(null)", p.Name ?? "(null)", p.ExternalId ?? "(null)");
			}

			return null;
		}

		_logger.LogInformation(
			"BuildPrivateStateAsync: Found player {PlayerName} at seat {SeatPosition} with {CardCount} cards (hand #{HandNumber})",
			gamePlayer.Player.Name, gamePlayer.SeatPosition,
			gamePlayer.Cards.Count(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber),
			game.CurrentHandNumber);

		var hand = BuildPrivateHand(gamePlayer, game.CurrentHandNumber, game.GameType?.Code);

		string? handEvaluationDescription = null;
		try
		{
			var selectedShowcaseDealOrder = IsBobBarkerGame(game.GameType?.Code)
				? BobBarkerVariantState.GetSelectedShowcaseDealOrder(gamePlayer)
				: null;

			var playerCardEntities = gamePlayer.Cards
				.Where(c => !c.IsDiscarded
					&& c.HandNumber == game.CurrentHandNumber
					&& (selectedShowcaseDealOrder is null || c.DealOrder != selectedShowcaseDealOrder.Value))
				.OrderBy(c => c.DealOrder)
				.ToList();

			var playerCards = playerCardEntities
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			var communityCards = await _context.GameCards
				.Where(c => c.GameId == gameId
						&& !c.IsDiscarded
						&& c.HandNumber == game.CurrentHandNumber
						&& c.Location == CardLocation.Community
						&& c.IsVisible)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToListAsync(cancellationToken);

			// Some variants persist dealt draw cards as `GameCards` rather than `GamePlayer.Cards`.
			// If so, treat them as the player's evaluation cards when they appear to be the only source.
			if (playerCards.Count == 0 && communityCards.Count >= 5)
			{
				playerCards = communityCards;
				communityCards = [];
			}

			var allEvaluationCards = playerCards.Concat(communityCards).ToList();

			var isSevenCardStud = IsStudGame(game.GameType?.Code);
			var isCommunityCardGame = IsHoldEmGame(game.GameType?.Code)
				|| IsHoldTheBaseballGame(game.GameType?.Code)
				|| IsOmahaGame(game.GameType?.Code)
				|| IsBobBarkerGame(game.GameType?.Code)
				|| IsNebraskaGame(game.GameType?.Code)
				|| IsSouthDakotaGame(game.GameType?.Code)
				|| IsIrishHoldEmGame(game.GameType?.Code);

			// Seven Card Stud / Baseball / Follow The Queen: Requires 2 hole + up to 4 board + 1 down card (7 total at showdown)
			if (isSevenCardStud)
			{
				handEvaluationDescription = await BuildStudVariantHandDescriptionAsync(game, gamePlayer, cancellationToken);
			}
			// Draw games (no community cards). Provide a description for any cards held.
			// (During seating / pre-deal phases there may be 0-4 cards and we keep the description null.)
			else if (!isCommunityCardGame && communityCards.Count == 0 && playerCards.Count > 0)
			{
				var drawHand = BuildDrawHandForGame(game.GameType?.Code, playerCards);
				handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(drawHand);
			}
			// Community-card games (start evaluating as soon as player has cards).
			else if (allEvaluationCards.Count >= 2)
			{
				// Good Bad Ugly: remaining hole cards + visible community with dynamic wild cards.
				// Check this before card-count branches because The Bad may discard hole cards,
				// leaving fewer than 4 and incorrectly matching other branches.
				if (IsGoodBadUglyGame(game.GameType?.Code))
				{
					// If the player has been eliminated by The Ugly, show dead hand
					if (string.Equals(gamePlayer.VariantState, "UGLY_ELIMINATED", StringComparison.OrdinalIgnoreCase))
					{
						handEvaluationDescription = "Dead Hand (The Ugly)";
					}
					else
					{
						int? wildRank = null;
						var goodCard = await _context.GameCards
							.Where(c => c.GameId == gameId
								&& c.HandNumber == game.CurrentHandNumber
								&& c.Location == CardLocation.Community
								&& c.DealtAtPhase == "TheGood"
								&& c.IsVisible)
							.AsNoTracking()
							.FirstOrDefaultAsync(cancellationToken);
						if (goodCard is not null)
						{
							wildRank = (int)goodCard.Symbol;
						}
						var gbuHand = new GoodBadUglyHand(playerCards.Concat(communityCards).ToList(), [], [], wildRank, new GoodBadUglyWildCardRules());
						handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(gbuHand);
					}
				}
				else
				{
					Card? visibleKlondikeCard = null;
					if (IsGameType(game.GameType?.Code, PokerGameMetadataRegistry.KlondikeCode))
					{
						var klondikeCardEntity = await _context.GameCards
							.Where(c => c.GameId == gameId
								&& c.HandNumber == game.CurrentHandNumber
								&& c.DealtAtPhase == "KlondikeCard")
							.AsNoTracking()
							.FirstOrDefaultAsync(cancellationToken);

						if (klondikeCardEntity is { IsVisible: true })
						{
							visibleKlondikeCard = new Card((Suit)klondikeCardEntity.Suit, (Symbol)klondikeCardEntity.Symbol);
						}
					}

					handEvaluationDescription = CommunityHandDescriptionEvaluator.Evaluate(
						game.GameType?.Code,
						playerCards,
						communityCards,
						visibleKlondikeCard);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogDebug(ex, "Failed to compute hand evaluation description for game {GameId}, player {PlayerName}", gameId, gamePlayer.Player.Name);
		}

		var isMyTurn = game.CurrentPlayerIndex == gamePlayer.SeatPosition;
		var availableActions = isMyTurn
			? await BuildAvailableActionsAsync(gameId, game, gamePlayer, cancellationToken)
			: null;

		// Get game rules for metadata
		GameRules? rules = null;
		if (PokerGameRulesRegistry.TryGet(game.GameType?.Code, out var r))
		{
			rules = r;
		}

		var draw = BuildDrawPrivateDto(game, gamePlayer, rules);
		var dropOrStay = BuildDropOrStayPrivateDto(game, gamePlayer);
		var buyCardOffer = BuildBuyCardOfferPrivateDto(game, gamePlayer);
		var tollboothOffer = await BuildTollboothOfferPrivateDto(game, gamePlayer, cancellationToken);

		// Get hand history personalized for this player
		var handHistory = await GetHandHistoryEntriesAsync(gameId, gamePlayer.PlayerId, take: 25, cancellationToken);

		// Build chip history from hand history, including cashier balance
		var cashierBalance = await _walletService.GetBalanceAsync(gamePlayer.PlayerId, cancellationToken);
		var chipHistory = BuildChipHistory(gamePlayer, handHistory, cashierBalance);

		return new PrivateStateDto
		{
			GameId = gameId,
			PlayerName = gamePlayer.Player.Name,
			SeatPosition = gamePlayer.SeatPosition,
			Hand = hand,
			HandEvaluationDescription = handEvaluationDescription,
			AvailableActions = availableActions,
			Draw = draw,
			DropOrStay = dropOrStay,
			BuyCardOffer = buyCardOffer,
			TollboothOffer = tollboothOffer,
			IsMyTurn = isMyTurn,
			HandHistory = handHistory,
			ChipHistory = chipHistory
		};
	}

	private List<CardPrivateDto> BuildPrivateHand(GamePlayer gamePlayer, int currentHandNumber, string? gameTypeCode)
	{
		// Filter cards by current hand number to naturally handle sitting out players.
		// - During Complete phase: player who just lost all chips still has cards from this hand
		// - During next hand: their old cards are deleted, so they'll have no cards
		var allCards = gamePlayer.Cards?.ToList() ?? [];
		var filteredCards = allCards
			.Where(c => !c.IsDiscarded && c.HandNumber == currentHandNumber);

		// Use street-aware ordering for Seven Card Stud to handle multi-street dealing correctly
		var isSevenCardStud = IsStudGame(gameTypeCode);
		var orderedCards = OrderCardsForDisplay(filteredCards, isSevenCardStud).ToList();

		_logger.LogDebug(
			"BuildPrivateHand for player {PlayerName}: {FilteredCount} cards for hand #{HandNumber}, ordered: [{OrderedCards}]",
			gamePlayer.Player.Name,
			orderedCards.Count,
			currentHandNumber,
			string.Join(", ", orderedCards.Select(c => $"{c.Symbol}{c.Suit}(DO={c.DealOrder},Phase={c.DealtAtPhase})")));

		var selectedShowcaseDealOrder = IsBobBarkerGame(gameTypeCode)
			? BobBarkerVariantState.GetSelectedShowcaseDealOrder(gamePlayer)
			: null;

		return orderedCards
			.Select(c => new CardPrivateDto
			{
				Rank = MapSymbolToRank(c.Symbol),
				Suit = c.Suit.ToString(),
				DealOrder = c.DealOrder,
				IsSelectedForDiscard = false,
				IsPubliclyVisible = c.IsVisible,
				IsShowcaseCard = selectedShowcaseDealOrder == c.DealOrder
			})
			.ToList();
	}

	private async Task<AvailableActionsDto?> BuildAvailableActionsAsync(
		Guid gameId,
		Game game,
		GamePlayer gamePlayer,
		CancellationToken cancellationToken)
	{
		// Only provide actions during betting phases
		// Includes Five Card Draw phases and Seven Card Stud street phases
		var bettingPhases = new[]
		{
			"FirstBettingRound",
			"SecondBettingRound",
			"ThirdStreet",
			"FourthStreet",
			"FifthStreet",
			"SixthStreet",
			"SeventhStreet",
			"PreFlop",
			"Flop",
			"Turn",
			"River"
		};
		if (!bettingPhases.Contains(game.CurrentPhase))
		{
			return null;
		}

		// No actions for folded or all-in players
		if (gamePlayer.HasFolded || gamePlayer.IsAllIn)
		{
			return null;
		}

		var bettingRound = await _context.BettingRounds
			.Where(br => br.GameId == gameId && br.HandNumber == game.CurrentHandNumber && !br.IsComplete)
			.AsNoTracking()
			.FirstOrDefaultAsync(cancellationToken);

		if (bettingRound is null)
		{
			return null;
		}

		var currentBet = bettingRound.CurrentBet;
		var minBet = bettingRound.MinBet;
		var lastRaiseAmount = bettingRound.LastRaiseAmount > 0 ? bettingRound.LastRaiseAmount : minBet;
		var amountToCall = currentBet - gamePlayer.CurrentBet;
		var canAffordCall = gamePlayer.ChipStack >= amountToCall;

		return new AvailableActionsDto
		{
			CanCheck = currentBet == gamePlayer.CurrentBet,
			CanBet = currentBet == 0 && gamePlayer.ChipStack >= minBet,
			CanCall = currentBet > gamePlayer.CurrentBet && canAffordCall && amountToCall < gamePlayer.ChipStack,
			CanRaise = currentBet > 0 && gamePlayer.ChipStack > amountToCall,
			CanFold = currentBet > gamePlayer.CurrentBet,
			CanAllIn = gamePlayer.ChipStack > 0,
			MinBet = minBet,
			MaxBet = gamePlayer.ChipStack,
			CallAmount = Math.Min(amountToCall, gamePlayer.ChipStack),
			MinRaise = currentBet + lastRaiseAmount
		};
	}

	private static DrawPrivateDto? BuildDrawPrivateDto(
		Game game,
		GamePlayer gamePlayer,
		GameRules? rules)
	{
		if (game.CurrentPhase != "DrawPhase")
		{
			return null;
		}

		// Check if player has an Ace in their current hand to allow 4 discards
		var playerCards = gamePlayer.Cards
			.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
			.ToList();

		var baseMaxDiscards = rules?.Drawing?.MaxDiscards ?? 3;
		var hasAce = playerCards.Any(c => c.Symbol == Entities.CardSymbol.Ace);

		// Traditional 5-card draw rules: 3 discards, or 4 if you have an Ace.
		// If the game rules specify a different MaxDiscards (like 5 in Kings and Lows), respect that.
		var maxDiscards = (baseMaxDiscards == 3 && hasAce) ? 4 : baseMaxDiscards;
		var isIrishHoldEm = IsIrishHoldEmGame(game.GameType?.Code);
		var isBobBarker = IsBobBarkerGame(game.GameType?.Code);
		var isEligibleIrishDiscardActor = gamePlayer.Status == GamePlayerStatus.Active
			&& !gamePlayer.HasFolded
			&& !gamePlayer.HasDrawnThisRound;
		var isEligibleBobBarkerSelector = gamePlayer.Status == GamePlayerStatus.Active
			&& !gamePlayer.HasFolded
			&& !gamePlayer.HasDrawnThisRound;

		return new DrawPrivateDto
		{
			IsMyTurnToDraw = isIrishHoldEm
				? isEligibleIrishDiscardActor
				: isBobBarker
					? isEligibleBobBarkerSelector
					: game.CurrentDrawPlayerIndex == gamePlayer.SeatPosition,
			MaxDiscards = maxDiscards,
			HasDrawnThisRound = gamePlayer.HasDrawnThisRound
		};
	}

	private static DropOrStayPrivateDto? BuildDropOrStayPrivateDto(
		Game game,
		GamePlayer gamePlayer)
	{
		if (game.CurrentPhase != "DropOrStay")
		{
			return null;
		}

		return new DropOrStayPrivateDto
		{
			IsMyTurnToDecide = game.CurrentPlayerIndex == gamePlayer.SeatPosition,
			HasDecidedThisRound = gamePlayer.DropOrStayDecision.HasValue &&
										  gamePlayer.DropOrStayDecision.Value != Entities.DropOrStayDecision.Undecided,
			Decision = gamePlayer.DropOrStayDecision?.ToString()
		};
	}

	private BuyCardOfferPrivateDto? BuildBuyCardOfferPrivateDto(Game game, GamePlayer gamePlayer)
	{
		if (!IsBaseballGame(game.GameType?.Code) ||
			!string.Equals(game.CurrentPhase, nameof(Phases.BuyCardOffer), StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		var buyCardState = BaseballGameSettings.GetState(game, game.MinBet ?? 0);
		if (buyCardState.PendingOffers.Count == 0)
		{
			return null;
		}

		var currentOffer = buyCardState.PendingOffers.First();
		if (currentOffer.PlayerId != gamePlayer.PlayerId)
		{
			return null;
		}

		var triggerCard = gamePlayer.Cards?.FirstOrDefault(c => c.Id == currentOffer.CardId);
		CardPublicDto? triggerCardDto = null;
		if (triggerCard is not null)
		{
			triggerCardDto = new CardPublicDto
			{
				IsFaceUp = true,
				Rank = MapSymbolToRank(triggerCard.Symbol),
				Suit = triggerCard.Suit.ToString(),
				DealOrder = triggerCard.DealOrder
			};
		}

		return new BuyCardOfferPrivateDto
		{
			BuyCardPrice = buyCardState.BuyCardPrice,
			TriggerCard = triggerCardDto,
			PendingOfferCount = buyCardState.PendingOffers.Count
		};
	}

	private async Task<TollboothOfferPrivateDto?> BuildTollboothOfferPrivateDto(
		Entities.Game game, GamePlayer gamePlayer, CancellationToken cancellationToken)
	{
		var gameTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code;
		if (!string.Equals(gameTypeCode, PokerGameMetadataRegistry.TollboothCode, StringComparison.OrdinalIgnoreCase)
			|| !string.Equals(game.CurrentPhase, nameof(Phases.TollboothOffer), StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		var ante = game.Ante ?? game.BringIn ?? game.SmallBet ?? game.MinBet ?? 0;

		var displayCards = await _context.GameCards
			.Where(c => c.GameId == game.Id
						&& c.Location == CardLocation.Community
						&& c.GamePlayerId == null
						&& c.HandNumber == game.CurrentHandNumber
						&& !c.IsDiscarded)
			.OrderBy(c => c.DealOrder)
			.AsNoTracking()
			.Select(c => new CardPublicDto
			{
				IsFaceUp = true,
				Rank = MapSymbolToRank(c.Symbol),
				Suit = c.Suit.ToString(),
				DealOrder = c.DealOrder
			})
			.ToListAsync(cancellationToken);

		return new TollboothOfferPrivateDto
		{
			IsMyTurnToChoose = game.CurrentDrawPlayerIndex == gamePlayer.SeatPosition
							   && !gamePlayer.HasFolded
							   && !gamePlayer.HasDrawnThisRound,
			HasChosenThisRound = gamePlayer.HasDrawnThisRound,
			FurthestCost = 0,
			NearestCost = ante,
			DeckCost = ante * 2,
			DisplayCards = displayCards
		};
	}

	/// <summary>
	/// Builds the chip history DTO for a player from hand history data.
	/// </summary>
	private ChipHistoryDto BuildChipHistory(
		GamePlayer gamePlayer,
		IReadOnlyList<CardGames.Contracts.SignalR.HandHistoryEntryDto> handHistory,
		int cashierBalance)
	{
		var entries = new List<ChipHistoryEntryDto>();

		// Take last 30 hands for the history window, sorted chronologically (oldest to newest).
		// The handHistory comes in descending order (newest first), so we need to reverse for proper chronological display.
		var recentHands = handHistory
			.OrderBy(h => h.CompletedAtUtc)
			.TakeLast(30)
			.ToList();

		// Calculate the starting stack for this window by working backwards from current chips
		var currentStack = gamePlayer.ChipStack;
		var totalDeltaInWindow = 0;

		foreach (var hand in recentHands)
		{
			var playerResult = hand.PlayerResults.FirstOrDefault(pr => pr.PlayerId == gamePlayer.PlayerId);
			if (playerResult != null)
			{
				totalDeltaInWindow += playerResult.NetAmount;
			}
		}

		// Starting point for the history window
		// If we have no hand history yet, OR if the first hand in our window is hand #1 (the very first hand),
		// use the player's actual starting chips to show the true baseline before any antes or bets.
		// Otherwise, calculate backwards from current stack.
		var isFirstHandInWindow = recentHands.FirstOrDefault()?.HandNumber == 1;
		var windowStartStack = (recentHands.Count == 0 || isFirstHandInWindow)
			? gamePlayer.StartingChips
			: currentStack - totalDeltaInWindow;

		// Add initial starting point entry to show the baseline
		// Use sequential hand numbering starting from 0 for the baseline
		entries.Add(new ChipHistoryEntryDto
		{
			HandNumber = 0,
			ChipStackAfterHand = windowStartStack,
			ChipsDelta = 0,
			Timestamp = recentHands.FirstOrDefault()?.CompletedAtUtc.AddSeconds(-1) ?? DateTimeOffset.UtcNow
		});

		var runningStack = windowStartStack;
		var sequentialHandNumber = 1; // Start from 1 after the baseline (0)

		// Build chip history entries for each completed hand
		// Use sequential hand numbers (1, 2, 3...) instead of actual database hand numbers
		foreach (var hand in recentHands)
		{
			var playerResult = hand.PlayerResults.FirstOrDefault(pr => pr.PlayerId == gamePlayer.PlayerId);
			if (playerResult != null)
			{
				runningStack += playerResult.NetAmount;
				entries.Add(new ChipHistoryEntryDto
				{
					HandNumber = sequentialHandNumber++,
					ChipStackAfterHand = runningStack,
					ChipsDelta = playerResult.NetAmount,
					Timestamp = hand.CompletedAtUtc
				});
			}
		}

		return new ChipHistoryDto
		{
			CurrentChips = gamePlayer.ChipStack,
			CashierBalance = cashierBalance,
			PendingChipsToAdd = gamePlayer.PendingChipsToAdd,
			StartingChips = gamePlayer.StartingChips,
			History = entries
		};
	}

	/// <summary>
	/// Retrieves face-up cards ordered correctly for Follow The Queen wild card determination.
	/// Accounts for dealing order relative to the Dealer button (Student dealing rotation).
	/// </summary>
	private async Task<List<Card>> GetOrderedFaceUpCardsAsync(
		Entities.Game game,
		CancellationToken cancellationToken)
	{
		var rawFaceUpCards = await _context.GameCards
			.Where(c => c.GameId == game.Id &&
						c.HandNumber == game.CurrentHandNumber &&
						c.IsVisible &&
						!c.IsDiscarded)
			.Include(c => c.GamePlayer)
			.Select(c => new
			{
				c.Symbol,
				c.Suit,
				c.DealtAtPhase,
				c.DealOrder,
				c.Location,
				SeatPosition = c.GamePlayer != null ? c.GamePlayer.SeatPosition : -1
			})
			.ToListAsync(cancellationToken);

		var seats = rawFaceUpCards
			.GroupBy(card => card.SeatPosition)
			.Select(group => new
			{
				SeatPosition = group.Key,
				Cards = StudOrderHelper.OrderPlayerCards(
					group,
					card => card.DealtAtPhase,
					card => card.Location == CardLocation.Hole,
					card => card.DealOrder)
			})
			.ToList();

		return StudOrderHelper.OrderFaceUpCardsInGlobalDealOrder(
				seats,
				game.DealerPosition,
				seat => seat.SeatPosition,
				seat => seat.Cards,
				_ => true)
			.Select(card => new Card((Suit)card.Suit, (Symbol)card.Symbol))
			.ToList();
	}

	private async Task<string?> BuildStudVariantHandDescriptionAsync(Game game, GamePlayer gamePlayer, CancellationToken cancellationToken)
	{
		var playerCardEntities = gamePlayer.Cards
			.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
			.OrderBy(c => c.DealOrder)
			.ToList();

		if (playerCardEntities.Count < 2)
		{
			return null;
		}

		var holeCardEntities = playerCardEntities.Where(c => c.Location == CardLocation.Hole).ToList();
		var boardCards = playerCardEntities
			.Where(c => c.Location == CardLocation.Board)
			.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
			.ToList();

		var initialHoleCards = holeCardEntities.Take(2)
			.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
			.ToList();

		if (initialHoleCards.Count < 2)
		{
			return null;
		}

		if (IsFollowTheQueenGame(game.GameType?.Code))
		{
			return await EvaluateFollowTheQueenHandDescriptionAsync(game, holeCardEntities, initialHoleCards, boardCards, cancellationToken);
		}

		if (IsPairPressureGame(game.GameType?.Code))
		{
			return await EvaluatePairPressureHandDescriptionAsync(game, holeCardEntities, initialHoleCards, boardCards, cancellationToken);
		}

		if (!string.IsNullOrWhiteSpace(game.GameType?.Code) && StudVariantEvaluators.TryGetValue(game.GameType.Code, out var evaluator))
		{
			return evaluator(holeCardEntities, boardCards);
		}

		return EvaluateSevenCardStudHandDescription(holeCardEntities, initialHoleCards, boardCards);
	}

	private async Task<string?> EvaluateFollowTheQueenHandDescriptionAsync(
		Game game,
		List<GameCard> holeCardEntities,
		List<Card> initialHoleCards,
		List<Card> openCards,
		CancellationToken cancellationToken)
	{
		var faceUpCardsInOrder = await GetOrderedFaceUpCardsAsync(game, cancellationToken);

		if (holeCardEntities.Count >= 3 && openCards.Count <= 4)
		{
			var downCard = new Card((Suit)holeCardEntities[2].Suit, (Symbol)holeCardEntities[2].Symbol);
			var fullHand = new FollowTheQueenHand(initialHoleCards, openCards, downCard, faceUpCardsInOrder);
			return HandDescriptionFormatter.GetHandDescription(fullHand);
		}

		var partialHand = new FollowTheQueenHand(initialHoleCards, openCards, null, faceUpCardsInOrder);
		return HandDescriptionFormatter.GetHandDescription(partialHand);
	}

	private async Task<string?> EvaluatePairPressureHandDescriptionAsync(
		Game game,
		List<GameCard> holeCardEntities,
		List<Card> initialHoleCards,
		List<Card> openCards,
		CancellationToken cancellationToken)
	{
		var faceUpCardsInOrder = await GetOrderedFaceUpCardsAsync(game, cancellationToken);
		var downCard = holeCardEntities.Count >= 3
			? new Card((Suit)holeCardEntities[2].Suit, (Symbol)holeCardEntities[2].Symbol)
			: null;

		var hand = new PairPressureHand(initialHoleCards, openCards, downCard, faceUpCardsInOrder);
		return HandDescriptionFormatter.GetHandDescription(hand);
	}
}
