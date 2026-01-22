using System;
using System.Text.Json;
using CardGames.Contracts.SignalR;
using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetHandHistory;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;
using Microsoft.EntityFrameworkCore;
using Entities = CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Builds table state snapshots for SignalR broadcasts.
/// </summary>
public sealed class TableStateBuilder : ITableStateBuilder
{
	private readonly CardsDbContext _context;
	private readonly IActionTimerService _actionTimerService;
	private readonly ILogger<TableStateBuilder> _logger;

	private sealed record UserProfile(string? FirstName, string? AvatarUrl);

	/// <summary>
	/// Initializes a new instance of the <see cref="TableStateBuilder"/> class.
	/// </summary>
	public TableStateBuilder(CardsDbContext context, IActionTimerService actionTimerService, ILogger<TableStateBuilder> logger)
	{
		_context = context;
		_actionTimerService = actionTimerService;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<TableStatePublicDto?> BuildPublicStateAsync(Guid gameId, CancellationToken cancellationToken = default)
	{
		var game = await _context.Games
			.Include(g => g.GameType)
			.Include(g => g.Pots)
			.AsNoTracking()
			.FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

		if (game is null)
		{
			_logger.LogWarning("Game {GameId} not found when building public state", gameId);
			return null;
		}

		var gamePlayers = await _context.GamePlayers
			.Where(gp => gp.GameId == gameId && gp.Status != Entities.GamePlayerStatus.Left)
			.Include(gp => gp.Player)
			.Include(gp => gp.Cards)
			.OrderBy(gp => gp.SeatPosition)
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		// Calculate total pot from current betting round contributions
		var totalPot = await CalculateTotalPotAsync(game, game.CurrentHandNumber, cancellationToken);

		// Build seat DTOs with cards hidden (face-down)
		// Enrich with Identity profile data (first name/avatar) when available.
		var userProfilesByEmail = await _context.Users
			.AsNoTracking()
			.Where(u => u.Email != null)
			.Select(u => new { Email = u.Email!, u.FirstName, u.AvatarUrl })
			.ToDictionaryAsync(
				u => u.Email,
				u => new UserProfile(u.FirstName, u.AvatarUrl),
				StringComparer.OrdinalIgnoreCase,
				cancellationToken);

		var seats = gamePlayers
			.Select(gp => BuildSeatPublicDto(gp, game.CurrentHandNumber, game.Ante ?? 0, game.GameType?.Code, userProfilesByEmail))
					.ToList();

		// Calculate results phase state
		var isResultsPhase = (game.CurrentPhase == "Complete" || game.CurrentPhase == "PotMatching") && game.HandCompletedAt.HasValue;
		int? secondsUntilNextHand = null;
		if (isResultsPhase && game.NextHandStartsAt.HasValue)
		{
			var remaining = game.NextHandStartsAt.Value - DateTimeOffset.UtcNow;
			secondsUntilNextHand = Math.Max(0, (int)remaining.TotalSeconds);
		}

		// Get hand history for the dashboard (limited to recent hands)
		var handHistory = await GetHandHistoryEntriesAsync(gameId, currentUserPlayerId: null, take: 25, cancellationToken);

		// Get game rules for phase metadata
		GameRules? rules = null;
		GamePhaseDescriptor? currentPhaseDescriptor = null;
		if (PokerGameRulesRegistry.TryGet(game.GameType?.Code, out var r))
		{
			rules = r;
			currentPhaseDescriptor = rules?.Phases
				.FirstOrDefault(p => p.PhaseId.Equals(game.CurrentPhase, StringComparison.OrdinalIgnoreCase));
		}

		// Build Player vs Deck state (if applicable)
		var playerVsDeck = await BuildPlayerVsDeckStateAsync(game, gamePlayers, userProfilesByEmail, cancellationToken);

		// Get action timer state
		var actionTimerState = _actionTimerService.GetTimerState(gameId);
		var actionTimer = actionTimerState is not null
			? new ActionTimerStateDto
			{
				SecondsRemaining = actionTimerState.SecondsRemaining,
				DurationSeconds = actionTimerState.DurationSeconds,
				StartedAtUtc = actionTimerState.StartedAtUtc,
				PlayerSeatIndex = actionTimerState.PlayerSeatIndex,
				IsActive = !actionTimerState.IsExpired
			}
			: null;

		return new TableStatePublicDto
		{
			GameId = game.Id,
			Name = game.Name,
			GameTypeName = game.GameType?.Name,
			GameTypeCode = game.GameType?.Code,
			CurrentPhase = game.CurrentPhase,
			CurrentPhaseDescription = PhaseDescriptionResolver.TryResolve(game.GameType?.Code, game.CurrentPhase),
			Ante = game.Ante ?? 0,
			MinBet = game.MinBet ?? 0,
			TotalPot = totalPot,
			DealerSeatIndex = game.DealerPosition,
			CurrentActorSeatIndex = game.CurrentPlayerIndex,
			IsPaused = game.Status == Entities.GameStatus.BetweenHands,
			CurrentHandNumber = game.CurrentHandNumber,
			CreatedByName = game.CreatedByName,
			Seats = seats,
			Showdown = BuildShowdownPublicDto(game, gamePlayers, userProfilesByEmail),
			HandCompletedAtUtc = game.HandCompletedAt,
			NextHandStartsAtUtc = game.NextHandStartsAt,
			IsResultsPhase = isResultsPhase,
			SecondsUntilNextHand = secondsUntilNextHand,
			HandHistory = handHistory,
			CurrentPhaseCategory = currentPhaseDescriptor?.Category,
			CurrentPhaseRequiresAction = currentPhaseDescriptor?.RequiresPlayerAction ?? false,
			CurrentPhaseAvailableActions = currentPhaseDescriptor?.AvailableActions,
			DrawingConfig = BuildDrawingConfigDto(rules),
			SpecialRules = BuildSpecialRulesDto(rules),
			PlayerVsDeck = playerVsDeck,
			ActionTimer = actionTimer
		};
	}

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

		var hand = BuildPrivateHand(gamePlayer, game.CurrentHandNumber);

		string? handEvaluationDescription = null;
		try
		{
			var playerCards = gamePlayer.Cards
				.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
				.OrderBy(c => c.DealOrder)
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			var communityCards = await _context.GameCards
				.Where(c => c.GameId == gameId
						&& !c.IsDiscarded
						&& c.HandNumber == game.CurrentHandNumber
						&& c.Location == CardLocation.Community)
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

			// Seven Card Stud: Requires 2 hole + up to 4 board + 1 down card (7 total at showdown)
			if (string.Equals(game.GameType?.Code, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase))
			{
				// Need to access the original cards with Location info
				var playerCardEntities = gamePlayer.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCardEntities = playerCardEntities.Where(c => c.Location == CardLocation.Hole).ToList();
				var boardCardEntities = playerCardEntities.Where(c => c.Location == CardLocation.Board).ToList();

				// Evaluate once we have at least 5 cards (Third Street: 2 hole + 1 board = 3, need to wait for 5+)
				if (playerCardEntities.Count >= 5)
				{
					var initialHoleCards = holeCardEntities.Take(2)
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();
					var openCards = boardCardEntities
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();

					// If we have 7 cards, the third hole card is the down card (seventh street)
					if (holeCardEntities.Count >= 3 && initialHoleCards.Count == 2 && openCards.Count <= 4)
					{
						var downCard = new Card((Suit)holeCardEntities[2].Suit, (Symbol)holeCardEntities[2].Symbol);
						var studHand = new SevenCardStudHand(initialHoleCards, openCards, downCard);
						handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(studHand);
					}
					else if (initialHoleCards.Count >= 2)
					{
						// Before seventh street, evaluate with a temporary StudHand using all available cards
						// Use the base StudHand which handles variable card counts
						var allHoleCards = holeCardEntities
							.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
							.ToList();
						var studHand = new StudHand(initialHoleCards, openCards, allHoleCards.Skip(2).ToList());
						handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(studHand);
					}
				}
			}
			// Draw games (no community cards). Provide a description once 5 cards are available.
			// (During seating / pre-deal phases there may be 0-4 cards and we keep the description null.)
			else if (communityCards.Count == 0 && playerCards.Count >= 5)
			{
				// Twos, Jacks, Man with the Axe uses wild cards.
				// Kings and Lows uses wild cards (Kings + lowest card).
				// The base `DrawHand` evaluator ignores wild substitutions,
				// so we must use the variant-specific hand type here.
				HandBase drawHand;
				if (string.Equals(game.GameType?.Code, "TWOSJACKSMANWITHTHEAXE", StringComparison.OrdinalIgnoreCase))
				{
					drawHand = new CardGames.Poker.Hands.DrawHands.TwosJacksManWithTheAxeDrawHand(playerCards);
				}
				else if (string.Equals(game.GameType?.Code, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
				{
					drawHand = new CardGames.Poker.Hands.DrawHands.KingsAndLowsDrawHand(playerCards);
				}
				else
				{
					drawHand = new DrawHand(playerCards);
				}

				handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(drawHand);
			}
			// Community-card games (need at least a 3-card board and 5+ total cards).
			else if (communityCards.Count >= 3 && allEvaluationCards.Count >= 5)
			{
				// Hold'em / Short-deck Hold'em style: 2 hole + up to 5 community
				if (playerCards.Count == 2)
				{
					var holdemHand = new CardGames.Poker.Hands.CommunityCardHands.HoldemHand(playerCards, communityCards);
					handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(holdemHand);
				}
				// Omaha style: 4 hole + up to 5 community
				else if (playerCards.Count == 4)
				{
					var omahaHand = new CardGames.Poker.Hands.CommunityCardHands.OmahaHand(playerCards, communityCards);
					handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(omahaHand);
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

		// Get hand history personalized for this player
		var handHistory = await GetHandHistoryEntriesAsync(gameId, gamePlayer.PlayerId, take: 25, cancellationToken);

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
			IsMyTurn = isMyTurn,
			HandHistory = handHistory
		};
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> GetPlayerUserIdsAsync(Guid gameId, CancellationToken cancellationToken = default)
	{
		var players = await _context.GamePlayers
			.Where(gp => gp.GameId == gameId)
			.Include(gp => gp.Player)
			.AsNoTracking()
			.Select(gp => new { gp.Player.Email, gp.Player.Name, gp.Player.ExternalId })
			.ToListAsync(cancellationToken);

		// Prefer Name over Email when Email appears malformed (e.g., contains multiple @ symbols).
		// This handles legacy data where Email may have been incorrectly stored with @localhost appended.
		var userIds = players
			.Select(p =>
			{
				// If Email looks like a valid single @ email, use it
				// Otherwise prefer Name (which is typically the clean email)
				var email = p.Email;
				var name = p.Name;

				// Check if email is malformed (has more than one @ symbol)
				var isMalformedEmail = !string.IsNullOrWhiteSpace(email) && email.Count(c => c == '@') > 1;

				if (isMalformedEmail && !string.IsNullOrWhiteSpace(name))
				{
					return name;
				}

				return email ?? name ?? p.ExternalId;
			})
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Cast<string>()
			.ToList();

		_logger.LogInformation(
			"GetPlayerUserIdsAsync for game {GameId}: {PlayerCount} players, UserIds: [{UserIds}], " +
			"Raw data: [{RawData}]",
			gameId,
			players.Count,
			string.Join(", ", userIds),
			string.Join(", ", players.Select(p => $"Email={p.Email ?? "(null)"}, Name={p.Name ?? "(null)"}, ExternalId={p.ExternalId ?? "(null)"}")));

		return userIds;
	}

	private static SeatPublicDto BuildSeatPublicDto(
		GamePlayer gamePlayer,
		int currentHandNumber,
		int ante,
		string? gameTypeCode,
		IReadOnlyDictionary<string, UserProfile> userProfilesByEmail)
	{
		var firstName = GetPlayerFirstName(gamePlayer, userProfilesByEmail);
		var avatarUrl = GetPlayerAvatarUrl(gamePlayer, userProfilesByEmail);
		var sittingOutReason = GetSittingOutReason(gamePlayer, ante, currentHandNumber);

		// Get current hand cards (not discarded)
		// Note: We filter by hand number to naturally handle sitting out players.
		// - During Complete phase: player who just lost all chips still has cards from this hand
		// - During next hand: their old cards are deleted, so they'll have no cards
		var playerCards = gamePlayer.Cards
			.Where(c => !c.IsDiscarded && c.HandNumber == currentHandNumber)
			.OrderBy(c => c.DealOrder)
			.ToList();

		// For Seven Card Stud, show visible cards; otherwise show face-down placeholders
		var isSevenCardStud = string.Equals(gameTypeCode, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase);

		var publicCards = playerCards.Select(card =>
		{
			// For stud games, respect the IsVisible flag; otherwise default to face-down
			var shouldShowCard = isSevenCardStud && card.IsVisible;

			return new CardPublicDto
			{
				IsFaceUp = shouldShowCard,
				Rank = shouldShowCard ? MapSymbolToRank(card.Symbol) : null,
				Suit = shouldShowCard ? GetCardSuitString(card.Suit) : null,
				DealOrder = card.DealOrder
			};
		}).ToList();

		return new SeatPublicDto
		{
			SeatIndex = gamePlayer.SeatPosition,
			IsOccupied = true,
			PlayerName = gamePlayer.Player.Name,
			PlayerFirstName = firstName,
			PlayerAvatarUrl = avatarUrl,
			Chips = gamePlayer.ChipStack,
			IsReady = gamePlayer.Status == Entities.GamePlayerStatus.Active && !gamePlayer.IsSittingOut,
			IsFolded = gamePlayer.HasFolded,
			IsAllIn = gamePlayer.IsAllIn,
			IsDisconnected = !gamePlayer.IsConnected,
			IsSittingOut = gamePlayer.IsSittingOut,
			SittingOutReason = sittingOutReason,
			CurrentBet = gamePlayer.CurrentBet,
			HasDecidedDropOrStay = gamePlayer.DropOrStayDecision.HasValue && gamePlayer.DropOrStayDecision.Value != Entities.DropOrStayDecision.Undecided,
			Cards = publicCards
		};
	}

	private static string? GetPlayerFirstName(GamePlayer gamePlayer, IReadOnlyDictionary<string, UserProfile> userProfilesByEmail)
	{
		if (!string.IsNullOrWhiteSpace(gamePlayer.Player.Email)
			&& userProfilesByEmail.TryGetValue(gamePlayer.Player.Email, out var profile))
		{
			return !string.IsNullOrWhiteSpace(profile.FirstName) ? profile.FirstName.Trim() : null;
		}
		return null;
	}

	private static string? GetPlayerAvatarUrl(GamePlayer gamePlayer, IReadOnlyDictionary<string, UserProfile> userProfilesByEmail)
	{
		string? avatarUrl = null;
		if (!string.IsNullOrWhiteSpace(gamePlayer.Player.Email)
			&& userProfilesByEmail.TryGetValue(gamePlayer.Player.Email, out var profile))
		{
			avatarUrl = profile.AvatarUrl;
		}

		avatarUrl ??= gamePlayer.Player.AvatarUrl;
		return !string.IsNullOrWhiteSpace(avatarUrl) ? avatarUrl.Trim() : null;
	}

	private static string? GetSittingOutReason(GamePlayer gamePlayer, int ante, int currentHandNumber)
	{
		if (gamePlayer.IsSittingOut)
		{
			if (gamePlayer.ChipStack < ante && ante > 0)
			{
				return "Insufficient chips";
			}
			return "Sitting out";
		}

		if (gamePlayer.HasFolded && gamePlayer.JoinedAtHandNumber == currentHandNumber)
		{
			return "Observing";
		}

		return null;
	}

	private List<CardPrivateDto> BuildPrivateHand(GamePlayer gamePlayer, int currentHandNumber)
	{
		// Filter cards by current hand number to naturally handle sitting out players.
		// - During Complete phase: player who just lost all chips still has cards from this hand
		// - During next hand: their old cards are deleted, so they'll have no cards
		var allCards = gamePlayer.Cards?.ToList() ?? [];
		var filteredCards = allCards
			.Where(c => !c.IsDiscarded && c.HandNumber == currentHandNumber)
			.ToList();

		_logger.LogInformation(
			"BuildPrivateHand for player {PlayerId}: Total cards in collection: {TotalCount}, " +
			"Cards for hand #{HandNumber}: {FilteredCount}, Card hand numbers: [{HandNumbers}]",
			gamePlayer.Id,
			allCards.Count,
			currentHandNumber,
			filteredCards.Count,
			string.Join(", ", allCards.Select(c => c.HandNumber.ToString())));

		return filteredCards
			.OrderBy(c => c.DealOrder)
			.Select(c => new CardPrivateDto
			{
				Rank = MapSymbolToRank(c.Symbol),
				Suit = c.Suit.ToString(),
				DealOrder = c.DealOrder,
				IsSelectedForDiscard = false
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
			"SeventhStreet"
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

		return new DrawPrivateDto
		{
			IsMyTurnToDraw = game.CurrentDrawPlayerIndex == gamePlayer.SeatPosition,
			MaxDiscards = maxDiscards,
			HasDrawnThisRound = gamePlayer.HasDrawnThisRound
		};
	}

	private static ShowdownPublicDto? BuildShowdownPublicDto(
		Game game,
		List<GamePlayer> gamePlayers,
		Dictionary<string, UserProfile> userProfilesByEmail)
	{
		if (game.CurrentPhase != "Showdown" && game.CurrentPhase != "Complete" && game.CurrentPhase != "PotMatching")
		{
			return null;
		}

		var isTwosJacksAxe = string.Equals(
			game.GameType?.Code,
			PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode,
			StringComparison.OrdinalIgnoreCase);

		var isSevenCardStud = string.Equals(
			game.GameType?.Code,
			PokerGameMetadataRegistry.SevenCardStudCode,
			StringComparison.OrdinalIgnoreCase);

		var isKingsAndLows = string.Equals(
			game.GameType?.Code,
			PokerGameMetadataRegistry.KingsAndLowsCode,
			StringComparison.OrdinalIgnoreCase);

		// Evaluate all hands for players who haven't folded
		// Use HandBase as the base type since all hand types inherit from it
		var playerHandEvaluations = new Dictionary<string, (HandBase hand, TwosJacksManWithTheAxeDrawHand? twosJacksHand, KingsAndLowsDrawHand? kingsAndLowsHand, SevenCardStudHand? studHand, GamePlayer gamePlayer, List<GameCard> cards, List<int> wildIndexes)>();

		foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
		{
			var cards = gp.Cards
				.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
				.OrderBy(c => c.DealOrder)
				.ToList();

			if (cards.Count >= 5)
			{
				var coreCards = cards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (isTwosJacksAxe)
				{
					var wildHand = new TwosJacksManWithTheAxeDrawHand(coreCards);
					// Find wild card indexes
					var wildIndexes = new List<int>();
					for (int i = 0; i < coreCards.Count; i++)
					{
						if (TwosJacksManWithTheAxeWildCardRules.IsWild(coreCards[i]))
						{
							wildIndexes.Add(i);
						}
					}
					playerHandEvaluations[gp.Player.Name] = (wildHand, wildHand, null, null, gp, cards, wildIndexes);
				}
				else if (isSevenCardStud)
				{
					// Seven Card Stud: 2 hole cards + 4 board cards + 1 down card = 7 cards
					// Hole cards are Location == Hole, board cards are Location == Board
					// The final hole card (seventh street) is the down card
					var holeCards = cards
						.Where(c => c.Location == CardLocation.Hole)
						.OrderBy(c => c.DealOrder)
						.ToList();
					var boardCards = cards
						.Where(c => c.Location == CardLocation.Board)
						.OrderBy(c => c.DealOrder)
						.ToList();

					// For Seven Card Stud: first 2 hole cards are initial hole cards, 
					// last hole card (if 3 exist) is the seventh street down card
					var initialHoleCards = holeCards.Take(2)
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();
					var openCards = boardCards
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();

					// SevenCardStudHand requires exactly 2 hole cards, up to 4 open cards, and 1 down card
					if (initialHoleCards.Count == 2 && openCards.Count <= 4 && holeCards.Count >= 3)
					{
						var downCard = new Card((Suit)holeCards[2].Suit, (Symbol)holeCards[2].Symbol);
						var studHand = new SevenCardStudHand(initialHoleCards, openCards, downCard);
						playerHandEvaluations[gp.Player.Name] = (studHand, null, null, studHand, gp, cards, []);
					}
				}
				else if (isKingsAndLows)
				{
					var kingsAndLowsHand = new KingsAndLowsDrawHand(coreCards);
					// Find wild card indexes using Kings and Lows rules (Kings are wild, plus lowest non-King cards)
					var wildCards = kingsAndLowsHand.WildCards;
					var wildIndexes = new List<int>();
					for (int i = 0; i < coreCards.Count; i++)
					{
						if (wildCards.Contains(coreCards[i]))
						{
							wildIndexes.Add(i);
						}
					}
					playerHandEvaluations[gp.Player.Name] = (kingsAndLowsHand, null, kingsAndLowsHand, null, gp, cards, wildIndexes);
				}
				else
				{
					var drawHand = new DrawHand(coreCards);
					playerHandEvaluations[gp.Player.Name] = (drawHand, null, null, null, gp, cards, []);
				}
			}
		}

		// Determine winners
		var highHandWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var sevensWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var sevensPoolRolledOver = false;

		// Extract actual payouts from pots if they have been awarded
		var actualPayouts = new Dictionary<string, (int Total, int Sevens, int High)>(StringComparer.OrdinalIgnoreCase);
		var awardedHandPots = game.Pots
			.Where(p => p.HandNumber == game.CurrentHandNumber && p.IsAwarded)
			.ToList();

		foreach (var pot in awardedHandPots)
		{
			if (string.IsNullOrWhiteSpace(pot.WinnerPayouts))
			{
				continue;
			}

			try
			{
				using var doc = JsonDocument.Parse(pot.WinnerPayouts);
				foreach (var element in doc.RootElement.EnumerateArray())
				{
					if (element.TryGetProperty("playerName", out var nameProp))
					{
						var name = nameProp.GetString();
						if (string.IsNullOrEmpty(name))
						{
							continue;
						}

						int amount = 0;
						if (element.TryGetProperty("amount", out var amountProp))
						{
							amount = amountProp.GetInt32();
						}

						int sevensAmount = 0;
						if (element.TryGetProperty("sevensAmount", out var sProp))
						{
							sevensAmount = sProp.GetInt32();
						}

						int highAmount = 0;
						if (element.TryGetProperty("highHandAmount", out var hProp))
						{
							highAmount = hProp.GetInt32();
						}

						if (actualPayouts.TryGetValue(name, out var existing))
						{
							actualPayouts[name] = (existing.Total + amount, existing.Sevens + sevensAmount, existing.High + highAmount);
						}
						else
						{
							actualPayouts[name] = (amount, sevensAmount, highAmount);
						}
					}
				}
			}
			catch (JsonException)
			{
				// Skip invalid JSON
			}
		}

		if (playerHandEvaluations.Count > 0)
		{
			// Determine high hand winners (highest hand strength)
			var maxStrength = playerHandEvaluations.Values.Max(h => h.hand.Strength);
			foreach (var kvp in playerHandEvaluations.Where(k => k.Value.hand.Strength == maxStrength))
			{
				highHandWinners.Add(kvp.Key);
			}

			// For Twos/Jacks/Axe, also determine sevens winners
			if (isTwosJacksAxe)
			{
				foreach (var kvp in playerHandEvaluations.Where(k => k.Value.twosJacksHand?.HasNaturalPairOfSevens() == true))
				{
					sevensWinners.Add(kvp.Key);
				}
				sevensPoolRolledOver = sevensWinners.Count == 0;
			}
		}
		else if (gamePlayers.Count(gp => !gp.HasFolded) == 1)
		{
			// Only one player remaining (won by fold)
			var winner = gamePlayers.First(gp => !gp.HasFolded);
			highHandWinners.Add(winner.Player.Name);
		}

		// Combined winners for IsWinner flag
		var allWinners = highHandWinners.Union(sevensWinners).ToHashSet(StringComparer.OrdinalIgnoreCase);

		var allLosers = isKingsAndLows
			? gamePlayers.Where(gp => !gp.HasFolded && !highHandWinners.Contains(gp.Player.Name))
				.Select(gp => gp.Player.Name)
				.ToList()
			: null;

		// Build player results
		var playerResults = gamePlayers
			.Where(gp => !gp.HasFolded)
			.Select(gp =>
			{
				var isWinner = allWinners.Contains(gp.Player.Name);
				var isSevensWinner = sevensWinners.Contains(gp.Player.Name);
				var isHighHandWinner = highHandWinners.Contains(gp.Player.Name);
				string? handRanking = null;
				List<int>? wildIndexes = null;

				if (playerHandEvaluations.TryGetValue(gp.Player.Name, out var eval))
				{
					handRanking = eval.hand.Type.ToString();
					wildIndexes = eval.wildIndexes.Count > 0 ? eval.wildIndexes : null;
				}

				userProfilesByEmail.TryGetValue(gp.Player.Email ?? string.Empty, out var userProfile);

				actualPayouts.TryGetValue(gp.Player.Name, out var payouts);

				return new ShowdownPlayerResultDto
				{
					PlayerName = gp.Player.Name,
					PlayerFirstName = userProfile?.FirstName,
					SeatPosition = gp.SeatPosition,
					HandRanking = handRanking,
					HandDescription = playerHandEvaluations.TryGetValue(gp.Player.Name, out var e)
						? HandDescriptionFormatter.GetHandDescription(e.hand)
						: null,
					AmountWon = payouts.Total,
					SevensAmountWon = payouts.Sevens,
					HighHandAmountWon = payouts.High,
					IsWinner = isWinner,
					IsSevensWinner = isSevensWinner,
					IsHighHandWinner = isHighHandWinner,
					WildCardIndexes = wildIndexes,
					Cards = gp.Cards
						.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
						.OrderBy(c => c.DealOrder)
						.Select(c => new CardPublicDto
						{
							IsFaceUp = true,
							Rank = MapSymbolToRank(c.Symbol),
							Suit = c.Suit.ToString()
						})
						.ToList()
				};
			})
			.OrderByDescending(r => r.IsWinner)
			.ThenByDescending(r => playerHandEvaluations.TryGetValue(r.PlayerName, out var e) ? e.hand.Strength : 0)
			.ToList();

		return new ShowdownPublicDto
		{
			PlayerResults = playerResults,
			IsComplete = game.CurrentPhase == "Complete",
			SevensWinners = isTwosJacksAxe ? sevensWinners.ToList() : null,
			HighHandWinners = isTwosJacksAxe ? highHandWinners.ToList() : null,
			Losers = allLosers,
			SevensPoolRolledOver = sevensPoolRolledOver
		};
	}

	private async Task<int> CalculateTotalPotAsync(Game game, int handNumber, CancellationToken cancellationToken)
	{
		// For Kings and Lows, the pot is tracked in the Pots table, not TotalContributedThisHand
		if (string.Equals(game.GameType?.Code, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
		{
			var total = await _context.Pots
				.Where(br => br.GameId == game.Id && br.HandNumber == handNumber)
				.AsNoTracking()
				.SumAsync(br => br.Amount, cancellationToken);

			_logger.LogDebug(
				"Kings and Lows pot calculation for game {GameId}, hand {HandNumber}: {TotalPot}",
				game.Id, handNumber, total);

			return total;
		}

		var totalContributions = await _context.GamePlayers
			.Where(gp => gp.GameId == game.Id)
			.SumAsync(gp => gp.TotalContributedThisHand, cancellationToken);

		return totalContributions;
	}

	private static string MapSymbolToRank(Entities.CardSymbol symbol)
	{
		return symbol switch
		{
			Entities.CardSymbol.Ace => "A",
			Entities.CardSymbol.King => "K",
			Entities.CardSymbol.Queen => "Q",
			Entities.CardSymbol.Jack => "J",
			Entities.CardSymbol.Ten => "10",
			Entities.CardSymbol.Nine => "9",
			Entities.CardSymbol.Eight => "8",
			Entities.CardSymbol.Seven => "7",
			Entities.CardSymbol.Six => "6",
			Entities.CardSymbol.Five => "5",
			Entities.CardSymbol.Four => "4",
			Entities.CardSymbol.Three => "3",
			Entities.CardSymbol.Deuce => "2",
			_ => symbol.ToString()
		};
	}

	private static string GetCardSuitString(Entities.CardSuit suit)
	{
		return suit switch
		{
			Entities.CardSuit.Hearts => "Hearts",
			Entities.CardSuit.Diamonds => "Diamonds",
			Entities.CardSuit.Spades => "Spades",
			Entities.CardSuit.Clubs => "Clubs",
			_ => suit.ToString()
		};
	}

	/// <summary>
	/// Retrieves hand history entries for the dashboard.
	/// </summary>
	private async Task<List<CardGames.Contracts.SignalR.HandHistoryEntryDto>> GetHandHistoryEntriesAsync(
		Guid gameId,
		Guid? currentUserPlayerId,
		int take,
		CancellationToken cancellationToken)
	{
		var histories = await _context.HandHistories
			.Include(h => h.Winners)
				.ThenInclude(w => w.Player)
			.Include(h => h.PlayerResults)
			.Where(h => h.GameId == gameId)
			.OrderByDescending(h => h.CompletedAtUtc)
			.Take(take)
			.AsSplitQuery()
			.AsNoTracking()
				.ToListAsync(cancellationToken);

		var winnerEmails = histories
			.SelectMany(h => h.Winners)
			.Select(w => w.Player.Email)
			.Where(email => !string.IsNullOrWhiteSpace(email))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var winnerFirstNamesByEmail = winnerEmails.Count == 0
			? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
			: await _context.Users
				.AsNoTracking()
				.Where(u => u.Email != null && winnerEmails.Contains(u.Email))
				.Select(u => new { Email = u.Email!, u.FirstName })
				.ToDictionaryAsync(u => u.Email, u => u.FirstName, StringComparer.OrdinalIgnoreCase, cancellationToken);

		_logger.LogInformation("GetHandHistoryEntriesAsync: Found {Count} histories for game {GameId}, currentUserPlayerId={PlayerId}",
			histories.Count, gameId, currentUserPlayerId);

		return histories.Select(h =>
		{
			_logger.LogInformation("HandHistory {HandNumber}: Winners.Count={WinnerCount}, PlayerResults.Count={PlayerResultCount}",
				h.HandNumber, h.Winners.Count, h.PlayerResults.Count);

			// Get winner display
			string GetWinnerFirstNameOrFallback()
			{
				var firstWinner = h.Winners.First();
				var email = firstWinner.Player.Email;

				if (!string.IsNullOrWhiteSpace(email) &&
					winnerFirstNamesByEmail.TryGetValue(email, out var firstName) &&
					!string.IsNullOrWhiteSpace(firstName))
				{
					return firstName;
				}

				return firstWinner.PlayerName;
			}

			var winnerDisplay = h.Winners.Count switch
			{
				0 => "Unknown",
				1 => GetWinnerFirstNameOrFallback(),
				_ => $"{GetWinnerFirstNameOrFallback()} +{h.Winners.Count - 1}"
			};

			_logger.LogInformation("HandHistory {HandNumber}: winnerDisplay='{WinnerDisplay}'", h.HandNumber, winnerDisplay);

			var totalWinnings = h.Winners.Sum(w => w.AmountWon);

			// Map all player results
			var playerResults = h.PlayerResults
				.OrderBy(pr => pr.SeatPosition)
				.Select(pr => new CardGames.Contracts.SignalR.PlayerHandResultDto
				{
					PlayerId = pr.PlayerId,
					PlayerName = pr.PlayerName,
					SeatPosition = pr.SeatPosition,
					ResultType = pr.ResultType.ToString(),
					ResultLabel = pr.GetResultLabel(),
					NetAmount = pr.NetChipDelta,
					ReachedShowdown = pr.ReachedShowdown,
					// TODO: Add visible cards from player's hole cards if reached showdown
					VisibleCards = pr.ReachedShowdown ? [] : null
				})
				.ToList();

			return new CardGames.Contracts.SignalR.HandHistoryEntryDto
			{
				HandNumber = h.HandNumber,
				CompletedAtUtc = h.CompletedAtUtc,
				WinnerName = winnerDisplay,
				AmountWon = totalWinnings,
				WinningHandDescription = h.WinningHandDescription,
				WonByFold = h.EndReason == Data.Entities.HandEndReason.FoldedToWinner,
				WinnerCount = h.Winners.Count,
				PlayerResults = playerResults
			};
		}).ToList();
	}

	/// <summary>
	/// Builds the drawing configuration DTO from game rules.
	/// </summary>
	private static DrawingConfigDto? BuildDrawingConfigDto(GameRules? rules)
	{
		if (rules?.Drawing is null)
		{
			return null;
		}

		return new DrawingConfigDto
		{
			AllowsDrawing = rules.Drawing.AllowsDrawing,
			MaxDiscards = rules.Drawing.MaxDiscards,
			SpecialRules = rules.Drawing.SpecialRules,
			DrawingRounds = rules.Drawing.DrawingRounds
		};
	}

	/// <summary>
	/// Builds the special rules DTO from game rules.
	/// </summary>
	private static GameSpecialRulesDto? BuildSpecialRulesDto(GameRules? rules)
	{
		if (rules?.SpecialRules is null || rules.SpecialRules.Count == 0)
		{
			return null;
		}

		return new GameSpecialRulesDto
		{
			HasDropOrStay = rules.SpecialRules.ContainsKey("DropOrStay"),
			HasPotMatching = rules.SpecialRules.ContainsKey("LosersMatchPot"),
			HasWildCards = rules.SpecialRules.ContainsKey("WildCards"),
			WildCardsDescription = rules.SpecialRules.TryGetValue("WildCards", out var wc)
				? wc?.ToString()
				: null,
			HasSevensSplit = rules.SpecialRules.ContainsKey("SevensSplit"),
			WildCardRules = BuildWildCardRulesDto(rules)
		};
	}

	/// <summary>
	/// Builds structured wild card rules from game rules.
	/// </summary>
	private static WildCardRulesDto? BuildWildCardRulesDto(GameRules? rules)
	{
		if (rules?.SpecialRules is null || !rules.SpecialRules.TryGetValue("WildCards", out var wildCardsValue))
		{
			return null;
		}

		var description = wildCardsValue?.ToString();
		var wildRanks = new List<string>();
		var specificCards = new List<string>();
		var lowestCardIsWild = false;

		// Parse known patterns into structured rules based on game type
		if (string.Equals(rules.GameTypeCode, "TWOSJACKSMANWITHTHEAXE", StringComparison.OrdinalIgnoreCase))
		{
			wildRanks.AddRange(["2", "J"]);
			specificCards.Add("KD"); // King of Diamonds
		}
		else if (string.Equals(rules.GameTypeCode, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
		{
			wildRanks.Add("K");
			lowestCardIsWild = true;
		}

		return new WildCardRulesDto
		{
			WildRanks = wildRanks.Count > 0 ? wildRanks : null,
			SpecificCards = specificCards.Count > 0 ? specificCards : null,
			LowestCardIsWild = lowestCardIsWild,
			Description = description
		};
	}

	/// <summary>
	/// Builds the Player vs Deck state for games where only one player stayed.
	/// </summary>
	private async Task<PlayerVsDeckStateDto?> BuildPlayerVsDeckStateAsync(
		Game game,
		List<GamePlayer> gamePlayers,
		IReadOnlyDictionary<string, UserProfile> userProfilesByEmail,
		CancellationToken cancellationToken)
	{
		// Only build for PlayerVsDeck phase
		if (!string.Equals(game.CurrentPhase, "PlayerVsDeck", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		// Get deck cards (cards with no GamePlayerId in this hand)
		var deckCards = await _context.GameCards
			.Where(gc => gc.GameId == game.Id
					 && gc.GamePlayerId == null
					 && gc.HandNumber == game.CurrentHandNumber
					 && !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		// Find the staying player
		var stayingPlayer = gamePlayers
			.FirstOrDefault(gp => gp.DropOrStayDecision == Entities.DropOrStayDecision.Stay);

		if (stayingPlayer is null)
		{
			_logger.LogWarning("No staying player found in PlayerVsDeck phase for game {GameId}", game.Id);
			return null;
		}

		// Determine decision maker: dealer, unless dealer is the staying player
		var dealerSeatPosition = game.DealerPosition;
		var orderedPlayers = gamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

		GamePlayer? decisionMaker = null;

		// Try dealer first
		var dealer = orderedPlayers.FirstOrDefault(gp => gp.SeatPosition == dealerSeatPosition);
		if (dealer is not null && dealer.PlayerId != stayingPlayer.PlayerId)
		{
			decisionMaker = dealer;
		}
		else
		{
			// Dealer is the staying player, find first player to dealer's left
			var dealerIndex = orderedPlayers.FindIndex(gp => gp.SeatPosition == dealerSeatPosition);
			if (dealerIndex < 0) dealerIndex = 0;

			for (int i = 1; i < orderedPlayers.Count; i++)
			{
				var nextIndex = (dealerIndex + i) % orderedPlayers.Count;
				var candidate = orderedPlayers[nextIndex];
				if (candidate.PlayerId != stayingPlayer.PlayerId &&
					candidate.Status == Entities.GamePlayerStatus.Active &&
					!candidate.IsSittingOut)
				{
					decisionMaker = candidate;
					break;
				}
			}
		}

		// Fallback: if no other player found, the staying player makes the decision
		decisionMaker ??= stayingPlayer;

		// Get decision maker's first name from user profile
		string? decisionMakerFirstName = null;
		if (!string.IsNullOrWhiteSpace(decisionMaker.Player?.Email) &&
			userProfilesByEmail.TryGetValue(decisionMaker.Player.Email, out var profile))
		{
			decisionMakerFirstName = profile.FirstName;
		}

		// Check if deck has drawn (by checking if any cards were dealt after the initial 5)
		// For simplicity, we'll track this via a flag or by checking draw records
		// For now, check if deck has exactly 5 cards and they haven't been modified
		var hasDeckDrawn = game.CurrentPhase != "PlayerVsDeck"; // If we've moved past, it's drawn

				return new PlayerVsDeckStateDto
				{
					DeckCards = deckCards.Select(c => new CardPublicDto
					{
						IsFaceUp = true,
						Rank = MapSymbolToRank(c.Symbol),
						Suit = c.Suit.ToString()
					}).ToList(),
					DecisionMakerSeatIndex = decisionMaker.SeatPosition,
					DecisionMakerName = decisionMaker.Player?.Name,
					DecisionMakerFirstName = decisionMakerFirstName,
					HasDeckDrawn = hasDeckDrawn,
					StayingPlayerName = stayingPlayer.Player?.Name,
					StayingPlayerSeatIndex = stayingPlayer.SeatPosition
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
		}
