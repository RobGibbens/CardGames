using System;
using System.Text.Json;
using CardGames.Contracts.SignalR;
using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetHandHistory;
using CardGames.Poker.Api.Features.Games.Baseball;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Betting;
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

			// DEBUG: Log card data before ordering for Seven Card Stud
			var isSevenCardStudGame = IsStudGame(game.GameType?.Code);
			if (isSevenCardStudGame)
			{
				foreach (var gp in gamePlayers)
				{
					var cardsForHand = gp.Cards
						.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
						.ToList();

					if (cardsForHand.Count > 0)
					{
						_logger.LogInformation(
							"[CARD-ORDER-DEBUG] Player {PlayerName} (Seat {Seat}), Phase {Phase}, Hand #{HandNumber}: " +
							"RAW cards before ordering: [{RawCards}]",
							gp.Player.Name,
							gp.SeatPosition,
							game.CurrentPhase,
							game.CurrentHandNumber,
							string.Join(", ", cardsForHand.Select(c =>
								$"{MapSymbolToRank(c.Symbol)}{c.Suit.ToString()[0]}(DO={c.DealOrder},Loc={c.Location},Phase={c.DealtAtPhase},Vis={c.IsVisible})")));

						// Also log the computed order keys
						var orderKeys = cardsForHand.Select(c => new
						{
							Card = $"{MapSymbolToRank(c.Symbol)}{c.Suit.ToString()[0]}",
							OrderKey = GetSevenCardStudOrderKey(c),
							c.DealOrder,
							c.Location,
							c.DealtAtPhase,
							c.IsVisible
						}).OrderBy(x => x.OrderKey).ToList();

						_logger.LogInformation(
							"[CARD-ORDER-DEBUG] Player {PlayerName}: Computed order keys: [{OrderKeys}]",
							gp.Player.Name,
							string.Join(", ", orderKeys.Select(x =>
								$"{x.Card}(Key={x.OrderKey},DO={x.DealOrder},Loc={x.Location},Phase={x.DealtAtPhase})")));
					}
				}
			}

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
				.Select(gp => BuildSeatPublicDto(gp, game.CurrentHandNumber, game.Ante ?? 0, game.GameType?.Code, game.CurrentPhase, userProfilesByEmail))
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

		// Build All-In Runout state (if applicable)
		var allInRunout = await BuildAllInRunoutStateAsync(game, gamePlayers, cancellationToken);

		// Build Chip Check Pause state (for Kings and Lows)
		var chipCheckPause = BuildChipCheckPauseState(game, gamePlayers, totalPot);

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
			Showdown = await BuildShowdownPublicDtoAsync(game, gamePlayers, userProfilesByEmail, cancellationToken),
			HandCompletedAtUtc = game.HandCompletedAt,
			NextHandStartsAtUtc = game.NextHandStartsAt,
			IsResultsPhase = isResultsPhase,
			SecondsUntilNextHand = secondsUntilNextHand,
			HandHistory = handHistory,
			CurrentPhaseCategory = currentPhaseDescriptor?.Category,
			CurrentPhaseRequiresAction = currentPhaseDescriptor?.RequiresPlayerAction ?? false,
			CurrentPhaseAvailableActions = currentPhaseDescriptor?.AvailableActions,
			DrawingConfig = BuildDrawingConfigDto(rules),
				SpecialRules = await BuildSpecialRulesDtoAsync(rules, game, cancellationToken),
				PlayerVsDeck = playerVsDeck,
			ActionTimer = actionTimer,
			AllInRunout = allInRunout,
			ChipCheckPause = chipCheckPause
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

		var hand = BuildPrivateHand(gamePlayer, game.CurrentHandNumber, game.GameType?.Code);

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

			var isBaseballGame = string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase);
			var isFollowTheQueenGame = string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase);
			var isSevenCardStud = IsStudGame(game.GameType?.Code);

			// Seven Card Stud / Baseball / Follow The Queen: Requires 2 hole + up to 4 board + 1 down card (7 total at showdown)
			if (isSevenCardStud)
			{
				// Need to access the original cards with Location info
				var playerCardEntities = gamePlayer.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCardEntities = playerCardEntities.Where(c => c.Location == CardLocation.Hole).ToList();
				var boardCardEntities = playerCardEntities.Where(c => c.Location == CardLocation.Board).ToList();

				// Evaluate even with partial hands (e.g. 2 hole + 0 board)
				if (playerCardEntities.Count >= 2)
				{
					var initialHoleCards = holeCardEntities.Take(2)
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();
					var openCards = boardCardEntities
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();

					if (isBaseballGame)
					{
						var allHoleCards = holeCardEntities
							.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
							.ToList();
						var baseballHand = new BaseballHand(allHoleCards, openCards, []);
						handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(baseballHand);
					}
					else if (isFollowTheQueenGame)
					{
						// Follow The Queen uses wild cards - get face-up cards for wild card determination
						var faceUpCardsInOrder = await GetOrderedFaceUpCardsAsync(game, cancellationToken);

						if (holeCardEntities.Count >= 3 && initialHoleCards.Count == 2 && openCards.Count <= 4)
						{
							var downCard = new Card((Suit)holeCardEntities[2].Suit, (Symbol)holeCardEntities[2].Symbol);
							var ftqHand = new FollowTheQueenHand(initialHoleCards, openCards, downCard, faceUpCardsInOrder);
							handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(ftqHand);
						}
						else if (initialHoleCards.Count >= 2)
						{
							// Partial hand (before 7th street)
							// For Follow The Queen, we must use the specific hand type to get wild card logic
							// The downCard parameter is nullable/optional in our modified constructor
							var ftqHand = new FollowTheQueenHand(initialHoleCards, openCards, null, faceUpCardsInOrder);
							handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(ftqHand);
						}
					}
					else if (holeCardEntities.Count >= 3 && initialHoleCards.Count == 2 && openCards.Count <= 4)
					{
						var downCard = new Card((Suit)holeCardEntities[2].Suit, (Symbol)holeCardEntities[2].Symbol);
						var studHand = new SevenCardStudHand(initialHoleCards, openCards, downCard);
						handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(studHand);
					}
					else if (initialHoleCards.Count >= 2)
					{
						var allHoleCards = holeCardEntities
							.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
							.ToList();
						var studHand = new StudHand(initialHoleCards, openCards, allHoleCards.Skip(2).ToList());
						handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(studHand);
					}
				}
			}
			// Draw games (no community cards). Provide a description for any cards held.
			// (During seating / pre-deal phases there may be 0-4 cards and we keep the description null.)
			else if (communityCards.Count == 0 && playerCards.Count > 0)
			{
				// Twos, Jacks, Man with the Axe uses wild cards.
				// Kings and Lows uses wild cards (Kings + lowest card).
				// The base `DrawHand` evaluator ignores wild substitutions,
				// so we must use the variant-specific hand type here.
				HandBase drawHand;
				if (string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode, StringComparison.OrdinalIgnoreCase))
				{
					drawHand = new CardGames.Poker.Hands.DrawHands.TwosJacksManWithTheAxeDrawHand(playerCards);
				}
				else if (string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.KingsAndLowsCode, StringComparison.OrdinalIgnoreCase))
				{
					drawHand = new CardGames.Poker.Hands.DrawHands.KingsAndLowsDrawHand(playerCards);
				}
				else
				{
					drawHand = new DrawHand(playerCards);
				}

				handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(drawHand);
			}
			// Community-card games (start evaluating as soon as player has cards).
			else if (allEvaluationCards.Count >= 2)
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
		var buyCardOffer = BuildBuyCardOfferPrivateDto(game, gamePlayer);

		// Get hand history personalized for this player
		var handHistory = await GetHandHistoryEntriesAsync(gameId, gamePlayer.PlayerId, take: 25, cancellationToken);

		// Build chip history from hand history
		var chipHistory = BuildChipHistory(gamePlayer, handHistory);

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
			IsMyTurn = isMyTurn,
			HandHistory = handHistory,
			ChipHistory = chipHistory
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
		string? currentPhase,
		IReadOnlyDictionary<string, UserProfile> userProfilesByEmail)
	{
		var firstName = GetPlayerFirstName(gamePlayer, userProfilesByEmail);
		var avatarUrl = GetPlayerAvatarUrl(gamePlayer, userProfilesByEmail);
		var sittingOutReason = GetSittingOutReason(gamePlayer, ante, currentHandNumber);

		// Check if we're in a showdown phase where cards should be revealed
		var isShowdownPhase = string.Equals(currentPhase, "Showdown", StringComparison.OrdinalIgnoreCase) ||
							  string.Equals(currentPhase, "Complete", StringComparison.OrdinalIgnoreCase) ||
							  string.Equals(currentPhase, "PotMatching", StringComparison.OrdinalIgnoreCase);

		// For Seven Card Stud, show visible cards; otherwise show face-down placeholders
		// During showdown phases, show cards face-up for players who haven't folded
		var isSevenCardStud = IsStudGame(gameTypeCode);

		// Get current hand cards (not discarded)
		// Note: We filter by hand number to naturally handle sitting out players.
		// - During Complete phase: player who just lost all chips still has cards from this hand
		// - During next hand: their old cards are deleted, so they'll have no cards
		var filteredCards = gamePlayer.Cards
			.Where(c => !c.IsDiscarded && c.HandNumber == currentHandNumber);

		// Use street-aware ordering for Seven Card Stud to handle multi-street dealing correctly
		var playerCards = OrderCardsForDisplay(filteredCards, isSevenCardStud).ToList();

		// During showdown, show cards for staying players (not folded)
		// Folded players should not have their cards revealed
		var shouldShowCardsForShowdown = isShowdownPhase && !gamePlayer.HasFolded;

		var publicCards = playerCards.Select(card =>
		{
			// For stud games, respect the IsVisible flag; otherwise default to face-down
			// During showdown, show cards face-up for staying players
			var shouldShowCard = (isSevenCardStud && card.IsVisible) || shouldShowCardsForShowdown;

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

		return orderedCards
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

	private async Task<ShowdownPublicDto?> BuildShowdownPublicDtoAsync(
		Game game,
		List<GamePlayer> gamePlayers,
		Dictionary<string, UserProfile> userProfilesByEmail,
		CancellationToken cancellationToken)
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

		var isBaseball = string.Equals(
			game.GameType?.Code,
			PokerGameMetadataRegistry.BaseballCode,
			StringComparison.OrdinalIgnoreCase);

		var isKingsAndLows = string.Equals(
			game.GameType?.Code,
			PokerGameMetadataRegistry.KingsAndLowsCode,
			StringComparison.OrdinalIgnoreCase);

		var isFollowTheQueen = string.Equals(
			game.GameType?.Code,
			PokerGameMetadataRegistry.FollowTheQueenCode,
			StringComparison.OrdinalIgnoreCase);

		// Evaluate all hands for players who haven't folded
		// Use HandBase as the base type since all hand types inherit from it
		var playerHandEvaluations = new Dictionary<string, (HandBase hand, TwosJacksManWithTheAxeDrawHand? twosJacksHand, KingsAndLowsDrawHand? kingsAndLowsHand, SevenCardStudHand? studHand, GamePlayer gamePlayer, List<GameCard> cards, List<int> wildIndexes, List<int> bestCardIndexes)>();

		foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
		{
			var filteredCards = gp.Cards
				.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber);
			var cards = OrderCardsForDisplay(filteredCards, isSevenCardStud || isBaseball || isFollowTheQueen).ToList();

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
					playerHandEvaluations[gp.Player.Name] = (wildHand, wildHand, null, null, gp, cards, wildIndexes, Enumerable.Range(0, cards.Count).ToList());
				}
				else if (isBaseball)
				{
					var holeCards = cards
						.Where(c => c.Location == CardLocation.Hole)
						.OrderBy(c => c.DealOrder)
						.ToList();
					var boardCards = cards
						.Where(c => c.Location == CardLocation.Board)
						.OrderBy(c => c.DealOrder)
						.ToList();

					var allHoleCards = holeCards
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();
					var openCards = boardCards
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();

					var baseballHand = new BaseballHand(allHoleCards, openCards, []);
					var wildCards = baseballHand.WildCards;
					var wildIndexes = new List<int>();
					for (int i = 0; i < coreCards.Count; i++)
					{
						if (wildCards.Contains(coreCards[i]))
						{
							wildIndexes.Add(i);
						}
					}
					playerHandEvaluations[gp.Player.Name] = (baseballHand, null, null, null, gp, cards, wildIndexes, GetCardIndexes(coreCards, baseballHand.BestHandSourceCards));
				}
				else if (isFollowTheQueen)
				{
					// Follow The Queen: Similar to Seven Card Stud but with wild cards
					var holeCards = cards
						.Where(c => c.Location == CardLocation.Hole)
						.OrderBy(c => c.DealOrder)
						.ToList();
					var boardCards = cards
						.Where(c => c.Location == CardLocation.Board)
						.OrderBy(c => c.DealOrder)
						.ToList();

					var initialHoleCards = holeCards.Take(2)
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();
					var openCards = boardCards
						.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
						.ToList();

					// Get face-up cards for wild card determination
					var faceUpCardsInOrder = await GetOrderedFaceUpCardsAsync(game, cancellationToken);

					if (initialHoleCards.Count == 2 && openCards.Count <= 4 && holeCards.Count >= 3)
					{
						var downCard = new Card((Suit)holeCards[2].Suit, (Symbol)holeCards[2].Symbol);
						var ftqHand = new FollowTheQueenHand(initialHoleCards, openCards, downCard, faceUpCardsInOrder);
						var wildCards = ftqHand.WildCards;
						var wildIndexes = new List<int>();
						for (int i = 0; i < coreCards.Count; i++)
						{
							if (wildCards.Contains(coreCards[i]))
							{
								wildIndexes.Add(i);
							}
						}
						playerHandEvaluations[gp.Player.Name] = (ftqHand, null, null, null, gp, cards, wildIndexes, GetCardIndexes(coreCards, ftqHand.BestHandSourceCards));
					}
					else if (initialHoleCards.Count >= 2)
					{
						// Partial hand (before 7th street)
						// For Follow The Queen, we must use the specific hand type to get wild card logic
						// The downCard parameter is nullable/optional in our modified constructor
						var ftqHand = new FollowTheQueenHand(initialHoleCards, openCards, null, faceUpCardsInOrder);
						var wildCards = ftqHand.WildCards;
						var wildIndexes = new List<int>();
						for (int i = 0; i < coreCards.Count; i++)
						{
							if (wildCards.Contains(coreCards[i]))
							{
								wildIndexes.Add(i);
							}
						}
						playerHandEvaluations[gp.Player.Name] = (ftqHand, null, null, null, gp, cards, wildIndexes, GetCardIndexes(coreCards, ftqHand.BestHandSourceCards));
					}
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
						playerHandEvaluations[gp.Player.Name] = (studHand, null, null, studHand, gp, cards, [], GetCardIndexes(coreCards, studHand.GetBestHand()));
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
					playerHandEvaluations[gp.Player.Name] = (kingsAndLowsHand, null, kingsAndLowsHand, null, gp, cards, wildIndexes, Enumerable.Range(0, cards.Count).ToList());
				}
				else
				{
					var drawHand = new DrawHand(coreCards);
					playerHandEvaluations[gp.Player.Name] = (drawHand, null, null, null, gp, cards, [], Enumerable.Range(0, cards.Count).ToList());
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
				List<int>? bestCardIndexes = null;

				if (playerHandEvaluations.TryGetValue(gp.Player.Name, out var eval))
				{
					handRanking = eval.hand.Type.ToString();
					wildIndexes = eval.wildIndexes.Count > 0 ? eval.wildIndexes : null;
					bestCardIndexes = eval.bestCardIndexes.Count > 0 ? eval.bestCardIndexes : null;
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
					BestCardIndexes = bestCardIndexes,
					Cards = OrderCardsForDisplay(
							gp.Cards.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber),
							isSevenCardStud || isBaseball || isFollowTheQueen)
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

		// For Kings and Lows player-vs-deck scenario, add the deck as a player in the results
		if (isKingsAndLows && playerResults.Count == 1)
		{
			// Check for deck cards (player-vs-deck scenario)
			var deckCards = await _context.GameCards
				.Where(c => c.GameId == game.Id &&
							c.HandNumber == game.CurrentHandNumber &&
							!c.IsDiscarded &&
							c.GamePlayerId == null &&
							c.Location == CardLocation.Board)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			if (deckCards.Count >= 5)
			{
				// Evaluate the deck's hand
				var deckCoreCards = deckCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();
				var deckHand = new KingsAndLowsDrawHand(deckCoreCards);

				// Get wild card indexes for the deck
				var deckWildCards = deckHand.WildCards;
				var deckWildIndexes = new List<int>();
				for (int i = 0; i < deckCoreCards.Count; i++)
				{
					if (deckWildCards.Contains(deckCoreCards[i]))
					{
						deckWildIndexes.Add(i);
					}
				}

				// Compare hands to determine winner
				var playerHand = playerHandEvaluations.Values.FirstOrDefault();
				var playerWins = playerHand.hand?.Strength >= deckHand.Strength; // Tie goes to player
				var deckWins = !playerWins;

				// Update the player's winner status if needed
				if (playerWins && playerResults.Count > 0)
				{
					var playerResult = playerResults[0];
					playerResults[0] = playerResult with { IsWinner = true };
					highHandWinners.Add(playerResult.PlayerName);
				}
				else if (deckWins && playerResults.Count > 0)
				{
					// Player loses to the deck
					var playerResult = playerResults[0];
					playerResults[0] = playerResult with { IsWinner = false };
					highHandWinners.Clear();
					allLosers = [playerResult.PlayerName];
				}

				// Add the deck as a "player" in the results
				var deckResult = new ShowdownPlayerResultDto
				{
					PlayerName = "The Deck",
					PlayerFirstName = "Deck",
					SeatPosition = -1, // Deck has no seat
					HandRanking = deckHand.Type.ToString(),
					HandDescription = HandDescriptionFormatter.GetHandDescription(deckHand),
					AmountWon = 0,
					SevensAmountWon = 0,
					HighHandAmountWon = 0,
					IsWinner = deckWins,
					IsSevensWinner = false,
					IsHighHandWinner = deckWins,
					WildCardIndexes = deckWildIndexes.Count > 0 ? deckWildIndexes : null,
					Cards = deckCards
						.Select(c => new CardPublicDto
						{
							IsFaceUp = true,
							Rank = MapSymbolToRank(c.Symbol),
							Suit = c.Suit.ToString()
						})
						.ToList()
				};

				playerResults.Add(deckResult);
			}
		}

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
		if (string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.KingsAndLowsCode, StringComparison.OrdinalIgnoreCase))
		{
			// In Kings and Lows, after a hand completes and losers match the pot,
			// the new pot is created with HandNumber = CurrentHandNumber + 1.
			// When the game is in Complete phase or paused for chip check,
			// we need to show the pot for the UPCOMING hand, not the current hand.
			var isWaitingForNextHand = game.CurrentPhase == "Complete" || 
									   game.CurrentPhase == "PotMatching" ||
									   game.IsPausedForChipCheck;

			var targetHandNumber = isWaitingForNextHand ? handNumber + 1 : handNumber;

			var total = await _context.Pots
				.Where(br => br.GameId == game.Id && br.HandNumber == targetHandNumber && !br.IsAwarded)
				.AsNoTracking()
				.SumAsync(br => br.Amount, cancellationToken);

			// If no pot found for next hand yet, check for unawarded pots from current hand
			// (e.g., if all players dropped and pot carries over)
			if (total == 0 && isWaitingForNextHand)
			{
				total = await _context.Pots
					.Where(br => br.GameId == game.Id && br.HandNumber == handNumber && !br.IsAwarded)
					.AsNoTracking()
					.SumAsync(br => br.Amount, cancellationToken);
			}

			_logger.LogDebug(
				"Kings and Lows pot calculation for game {GameId}, currentHand={HandNumber}, targetHand={TargetHand}, isWaiting={IsWaiting}: {TotalPot}",
				game.Id, handNumber, targetHandNumber, isWaitingForNextHand, total);

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

		// Get all player IDs from the histories
		var allPlayerIds = histories
			.SelectMany(h => h.PlayerResults)
			.Select(pr => pr.PlayerId)
			.Distinct()
			.ToList();

		_logger.LogInformation("[HANDHISTORY-NAMES] Loading player names/emails for {PlayerCount} player IDs: {PlayerIds}",
			allPlayerIds.Count, string.Join(", ", allPlayerIds.Take(5)));

		// Load all players separately to get Names and Emails
		var playersData = await _context.Players
			.Where(p => allPlayerIds.Contains(p.Id))
			.AsNoTracking()
			.Select(p => new { p.Id, p.Name, p.Email })
			.ToListAsync(cancellationToken);

		var playersByIdLookup = playersData.ToDictionary(p => p.Id, p => (Name: p.Name, Email: p.Email));

		_logger.LogInformation("[HANDHISTORY-NAMES] Loaded {PlayerCount} player names from Players table", playersByIdLookup.Count);
		foreach (var kvp in playersByIdLookup)
		{
			_logger.LogInformation("[HANDHISTORY-NAMES] Player {PlayerId} -> Name: '{PlayerName}', Email: '{Email}'", kvp.Key, kvp.Value.Name, kvp.Value.Email);
		}

		// Cards are now stored in HandHistoryPlayerResult.ShowdownCards (JSON)
		// No need to query GameCards table
		_logger.LogInformation("[HANDHISTORY-CARDS] Cards will be loaded from stored ShowdownCards in HandHistoryPlayerResult");

		var allEmails = playersData
			.Select(p => p.Email)
			.Concat(histories.SelectMany(h => h.Winners).Select(w => w.Player.Email))
			.Where(email => !string.IsNullOrWhiteSpace(email))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var userFirstNamesByEmail = allEmails.Count == 0
			? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
			: await _context.Users
				.AsNoTracking()
				.Where(u => u.Email != null && allEmails.Contains(u.Email))
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
					userFirstNamesByEmail.TryGetValue(email, out var firstName) &&
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
				.Select(pr =>
				{
					// Get player's actual name from Players lookup, fallback to stored name if not available
					var foundInLookup = playersByIdLookup.TryGetValue(pr.PlayerId, out var playerInfo);
					var playerName = foundInLookup ? playerInfo.Name : pr.PlayerName;

					// Try getting first name (real name) via email if available
					if (foundInLookup &&
						!string.IsNullOrWhiteSpace(playerInfo.Email) &&
						userFirstNamesByEmail.TryGetValue(playerInfo.Email, out var firstName) &&
						!string.IsNullOrWhiteSpace(firstName))
					{
						playerName = firstName;
					}

					if (!foundInLookup)
					{
						_logger.LogWarning("[HANDHISTORY-NAMES] Player ID {PlayerId} not found in lookup, using stored name: '{StoredName}'",
							pr.PlayerId, pr.PlayerName);
					}

					// Get cards for this player if they reached showdown
					List<string>? visibleCards = null;
					if (pr.ReachedShowdown && !string.IsNullOrWhiteSpace(pr.ShowdownCards))
					{
						try
						{
							visibleCards = System.Text.Json.JsonSerializer.Deserialize<List<string>>(pr.ShowdownCards);
							if (visibleCards != null && visibleCards.Any())
							{
								_logger.LogInformation("[HANDHISTORY-CARDS] ✓ Hand #{HandNumber}, Player '{PlayerName}' (Seat {Seat}): Found {CardCount} cards from ShowdownCards: {Cards}",
									h.HandNumber, playerName, pr.SeatPosition, visibleCards.Count, string.Join(", ", visibleCards));
							}
							else
							{
								_logger.LogWarning("[HANDHISTORY-CARDS] ✗ Hand #{HandNumber}, Player '{PlayerName}' (Seat {Seat}): ShowdownCards deserialized but empty",
									h.HandNumber, playerName, pr.SeatPosition);
							}
						}
						catch (System.Text.Json.JsonException ex)
						{
							_logger.LogError(ex, "[HANDHISTORY-CARDS] ✗ Hand #{HandNumber}, Player '{PlayerName}': Failed to deserialize ShowdownCards: {Json}",
								h.HandNumber, playerName, pr.ShowdownCards);
						}
					}
					else if (pr.ReachedShowdown)
					{
						_logger.LogWarning("[HANDHISTORY-CARDS] ✗ Hand #{HandNumber}, Player '{PlayerName}' (Seat {Seat}, PlayerId {PlayerId}): Reached showdown but ShowdownCards is null/empty",
							h.HandNumber, playerName, pr.SeatPosition, pr.PlayerId);
					}

					return new CardGames.Contracts.SignalR.PlayerHandResultDto
					{
						PlayerId = pr.PlayerId,
						PlayerName = playerName,
						SeatPosition = pr.SeatPosition,
						ResultType = pr.ResultType.ToString(),
						ResultLabel = pr.GetResultLabel(),
						NetAmount = pr.NetChipDelta,
						ReachedShowdown = pr.ReachedShowdown,
						VisibleCards = visibleCards
					};
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
	/// Builds the special rules DTO from game rules, with dynamic wild card computation for Follow the Queen.
	/// </summary>
	private async Task<GameSpecialRulesDto?> BuildSpecialRulesDtoAsync(
		GameRules? rules,
		Entities.Game game,
		CancellationToken cancellationToken)
	{
		if (rules?.SpecialRules is null || rules.SpecialRules.Count == 0)
		{
			return null;
		}

		var isFollowTheQueen = string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase);

		// For Follow the Queen, compute dynamic wild ranks from face-up cards
		IReadOnlyList<string>? dynamicWildRanks = null;
		if (isFollowTheQueen)
		{
			dynamicWildRanks = await ComputeFollowTheQueenWildRanksAsync(game, cancellationToken);
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
			WildCardRules = BuildWildCardRulesDto(rules, dynamicWildRanks)
		};
	}

	/// <summary>
	/// Computes the current wild card ranks for Follow the Queen based on face-up cards dealt so far.
	/// Queens are always wild. The rank following the last face-up Queen is also wild.
	/// </summary>
	private async Task<IReadOnlyList<string>> ComputeFollowTheQueenWildRanksAsync(
		Entities.Game game,
		CancellationToken cancellationToken)
	{
		var sortedFaceUpCards = await GetOrderedFaceUpCardsAsync(game, cancellationToken);

		var wildRanks = new List<string> { "Q" }; // Queens are always wild

		int? followingWildSymbol = null;
		for (var i = 0; i < sortedFaceUpCards.Count; i++)
		{
			if (sortedFaceUpCards[i].Symbol == Symbol.Queen)
			{
				if (i + 1 < sortedFaceUpCards.Count)
				{
					followingWildSymbol = (int)sortedFaceUpCards[i + 1].Symbol;
				}
				else
				{
					// Queen is the last face-up card, no following wild rank
					followingWildSymbol = null;
				}
			}
		}

		if (followingWildSymbol.HasValue)
		{
			var followRank = MapSymbolToRank((Entities.CardSymbol)followingWildSymbol.Value);
			if (followRank is not null)
			{
				wildRanks.Add(followRank);
			}
		}

		return wildRanks;
	}

	/// <summary>
	/// Builds the special rules DTO from game rules (static version for non-dynamic games).
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
	/// <param name="rules">The game rules.</param>
	/// <param name="dynamicWildRanks">Optional pre-computed wild ranks for dynamic wild card games (e.g., Follow the Queen).</param>
	private static WildCardRulesDto? BuildWildCardRulesDto(GameRules? rules, IReadOnlyList<string>? dynamicWildRanks = null)
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
		if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode, StringComparison.OrdinalIgnoreCase))
		{
			wildRanks.AddRange(["2", "J"]);
			specificCards.Add("KD"); // King of Diamonds
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase))
		{
			wildRanks.AddRange(["3", "9"]);
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.KingsAndLowsCode, StringComparison.OrdinalIgnoreCase))
		{
			wildRanks.Add("K");
			lowestCardIsWild = true;
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase))
		{
			// Follow the Queen: Queens are always wild, plus the dynamic "follow" rank
			if (dynamicWildRanks is { Count: > 0 })
			{
				wildRanks.AddRange(dynamicWildRanks);
			}
			else
			{
				wildRanks.Add("Q"); // Fallback: at minimum Queens are always wild
			}
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

		// Get deck cards (cards with no GamePlayerId in this hand, on the Board)
		// The deck's hand is stored as cards with no GamePlayerId and Location = Board
		var deckCards = await _context.GameCards
			.Where(gc => gc.GameId == game.Id
					 && gc.GamePlayerId == null
					 && gc.HandNumber == game.CurrentHandNumber
					 && gc.Location == Entities.CardLocation.Board
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

		// Get the staying player's cards
		var stayingPlayerCards = await _context.GameCards
			.Where(gc => gc.GameId == game.Id
					 && gc.GamePlayerId == stayingPlayer.Id
					 && gc.HandNumber == game.CurrentHandNumber
					 && gc.Location == Entities.CardLocation.Hand
					 && !gc.IsDiscarded)
			.OrderBy(gc => gc.DealtAt)
			.ThenBy(gc => gc.DealOrder)
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		// Evaluate the staying player's hand for the description
		string? stayingPlayerHandDescription = null;
		if (stayingPlayerCards.Count >= 5)
		{
			try
			{
				var cards = stayingPlayerCards
					.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
					.ToList();
				var kingsAndLowsHand = new KingsAndLowsDrawHand(cards);
				stayingPlayerHandDescription = HandDescriptionFormatter.GetHandDescription(kingsAndLowsHand);
			}
			catch
			{
				// Ignore evaluation errors
			}
		}

		// Evaluate the deck's hand for the description
		string? deckHandDescription = null;
		if (deckCards.Count >= 5)
		{
			try
			{
				var cards = deckCards
					.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
					.ToList();
				var kingsAndLowsHand = new KingsAndLowsDrawHand(cards);
				deckHandDescription = HandDescriptionFormatter.GetHandDescription(kingsAndLowsHand);
			}
			catch
			{
				// Ignore evaluation errors
			}
		}

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
			StayingPlayerSeatIndex = stayingPlayer.SeatPosition,
			StayingPlayerCards = stayingPlayerCards.Select(c => new CardPublicDto
			{
				IsFaceUp = true,
				Rank = MapSymbolToRank(c.Symbol),
				Suit = c.Suit.ToString()
			}).ToList(),
			StayingPlayerHandDescription = stayingPlayerHandDescription,
			DeckHandDescription = deckHandDescription
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
		if (!string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase) ||
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

	/// <summary>
	/// Builds the chip history DTO for a player from hand history data.
	/// </summary>
	private ChipHistoryDto BuildChipHistory(
		GamePlayer gamePlayer,
		IReadOnlyList<CardGames.Contracts.SignalR.HandHistoryEntryDto> handHistory)
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
			PendingChipsToAdd = gamePlayer.PendingChipsToAdd,
			StartingChips = gamePlayer.StartingChips,
			History = entries
		};
	}

	/// <summary>
	/// Builds the All-In Runout state for games where all players went all-in
	/// and remaining streets were dealt without betting.
	/// </summary>
	private async Task<AllInRunoutStateDto?> BuildAllInRunoutStateAsync(
		Game game,
		List<GamePlayer> gamePlayers,
		CancellationToken cancellationToken)
	{
		// Only build for Showdown phase
		if (!string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		// Check if this is an all-in runout by reading from GameSettings
		if (string.IsNullOrEmpty(game.GameSettings))
		{
			return null;
		}

		try
		{
			using var settingsDoc = JsonDocument.Parse(game.GameSettings);
			var root = settingsDoc.RootElement;

			// Check for allInRunout flag
			if (!root.TryGetProperty("allInRunout", out var allInRunoutProp) ||
				!allInRunoutProp.GetBoolean())
			{
				return null;
			}

			// Verify this is for the current hand
			if (root.TryGetProperty("runoutHandNumber", out var handNumberProp) &&
				handNumberProp.GetInt32() != game.CurrentHandNumber)
			{
				return null;
			}

			// Get the streets that were dealt during the runout
			var runoutStreets = new List<string>();
			if (root.TryGetProperty("runoutStreets", out var streetsProp) &&
				streetsProp.ValueKind == JsonValueKind.Array)
			{
				foreach (var street in streetsProp.EnumerateArray())
				{
					runoutStreets.Add(street.GetString() ?? "");
				}
			}

			if (runoutStreets.Count == 0)
			{
				return null;
			}

			// Get the timestamp when the runout occurred
			DateTimeOffset? runoutTimestamp = null;
			if (root.TryGetProperty("runoutTimestamp", out var timestampProp))
			{
				var timestampStr = timestampProp.GetString();
				if (!string.IsNullOrEmpty(timestampStr) &&
					DateTimeOffset.TryParse(timestampStr, out var parsed))
				{
					runoutTimestamp = parsed;
				}
			}

			// Get players who received cards (not folded)
			var activePlayersInHand = gamePlayers
				.Where(gp => !gp.HasFolded)
				.OrderBy(gp => gp.SeatPosition)
				.ToList();

			// Build runout cards by seat
			var runoutCardsBySeat = new Dictionary<int, IReadOnlyList<CardPublicDto>>();

			foreach (var player in activePlayersInHand)
			{
				// Get cards dealt during the runout streets for this player
				var runoutCards = await _context.GameCards
					.Where(gc => gc.GameId == game.Id
							 && gc.GamePlayerId == player.Id
							 && gc.HandNumber == game.CurrentHandNumber
							 && gc.DealtAtPhase != null
							 && runoutStreets.Contains(gc.DealtAtPhase)
							 && !gc.IsDiscarded)
					.OrderBy(gc => gc.DealtAt)
					.ThenBy(gc => gc.DealOrder)
					.AsNoTracking()
					.ToListAsync(cancellationToken);

				if (runoutCards.Count > 0)
				{
					runoutCardsBySeat[player.SeatPosition] = runoutCards.Select(c => new CardPublicDto
					{
						IsFaceUp = c.IsVisible,
						Rank = MapSymbolToRank(c.Symbol),
						Suit = c.Suit.ToString(),
						DealOrder = c.DealOrder
					}).ToList();
				}
			}

			// Map street names to friendly descriptions
			var streetDescriptions = new Dictionary<string, string>
									{
										{ "FourthStreet", "Fourth Street" },
										{ "FifthStreet", "Fifth Street" },
										{ "SixthStreet", "Sixth Street" },
										{ "SeventhStreet", "Seventh Street (River)" }
									};

			var currentStreet = runoutStreets.LastOrDefault();
			var currentStreetDescription = currentStreet != null && streetDescriptions.TryGetValue(currentStreet, out var desc)
				? desc
				: currentStreet;

			return new AllInRunoutStateDto
			{
				IsActive = true,
				CurrentStreet = currentStreet,
				CurrentStreetDescription = currentStreetDescription,
				TotalStreets = runoutStreets.Count,
				StreetsDealt = runoutCardsBySeat.Count,
				RunoutCardsBySeat = runoutCardsBySeat,
				CurrentDealingSeatIndex = -1, // Dealing complete
				IsComplete = true
			};
		}
		catch (JsonException ex)
		{
			_logger.LogWarning(ex, "Failed to parse GameSettings JSON for game {GameId}", game.Id);
			return null;
		}
	}

	/// <summary>
	/// Builds the chip check pause state for Kings and Lows games.
	/// </summary>
	private static ChipCheckPauseStateDto? BuildChipCheckPauseState(
		Game game,
				List<GamePlayer> gamePlayers,
				int currentPot)
	{
		if (!game.IsPausedForChipCheck)
		{
			return null;
		}

		var shortPlayers = gamePlayers
			.Where(gp => gp.Status == Entities.GamePlayerStatus.Active &&
						 !gp.IsSittingOut &&
						 gp.ChipStack < currentPot &&
						 !gp.AutoDropOnDropOrStay)
			.Select(gp => new ShortPlayerDto
			{
				SeatIndex = gp.SeatPosition,
				PlayerName = gp.Player?.Name ?? $"Seat {gp.SeatPosition}",
				PlayerFirstName = gp.Player?.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
				CurrentChips = gp.ChipStack,
				ChipsNeeded = currentPot - gp.ChipStack
			})
			.ToList();

		return new ChipCheckPauseStateDto
		{
			IsPaused = true,
			PauseStartedAt = game.ChipCheckPauseStartedAt,
			PauseEndsAt = game.ChipCheckPauseEndsAt,
			PotAmountToCover = currentPot,
			ShortPlayers = shortPlayers
		};
	}

	/// <summary>
	/// Gets the deal order for a Seven Card Stud street phase.
	/// Used to ensure consistent card ordering based on when cards were dealt during the hand.
	/// </summary>
	/// <param name="phase">The street/phase name (e.g., "ThirdStreet", "FourthStreet").</param>
	/// <returns>A numeric order value (1-5 for streets, 99 for unknown phases).</returns>
	private static int GetStreetPhaseOrder(string? phase) => phase switch
	{
		"ThirdStreet" => 1,
		"FourthStreet" => 2,
		"FifthStreet" => 3,
		"SixthStreet" => 4,
		"SeventhStreet" => 5,
		_ => 99 // Unknown phases sort last, not first
	};

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
				SeatPosition = c.GamePlayer != null ? c.GamePlayer.SeatPosition : -1
			})
			.ToListAsync(cancellationToken);

		// Sort in memory to handle rotation logic (Stud deals start left of Dealer)
		// IMPORTANT: DealOrder is not guaranteed to be globally unique across streets for stud variants.
		// To determine the "next card after the last face-up Queen" we must sort by street/phase first,
		// then by within-street order, then by rotation relative to the dealer.
		// Rotation order desired: (Dealer+1)...Max, 0...Dealer
		return rawFaceUpCards
			.OrderBy(c => GetStreetPhaseOrder(c.DealtAtPhase))
			.ThenBy(c => c.DealOrder)
			.ThenBy(c => c.SeatPosition > game.DealerPosition ? c.SeatPosition : c.SeatPosition + 1000)
			.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
			.ToList();
	}

	/// <summary>
	/// Generates a unique "key" for sorting Seven Card Stud cards.
	/// </summary>
	/// <remarks>
	/// Seven Card Stud display order:
	/// - ThirdStreet: 2 hole cards (face down), then 1 board card (face up) = positions 1, 2, 3
	/// - FourthStreet: 1 board card (face up) = position 4
	/// - FifthStreet: 1 board card (face up) = position 5
	/// - SixthStreet: 1 board card (face up) = position 6
	/// - SeventhStreet: 1 hole card (face down) = position 7
	/// 
	/// This method uses Location to distinguish hole from board cards within ThirdStreet,
	/// ensuring correct order even if DealOrder values are corrupted.
	/// </remarks>
	private static bool IsStudGame(string? gameTypeCode)
	{
		return string.Equals(gameTypeCode, PokerGameMetadataRegistry.SevenCardStudCode, StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(gameTypeCode, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(gameTypeCode, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase);
	}

	private static int GetSevenCardStudOrderKey(GameCard card)
	{
		var phaseOrder = GetStreetPhaseOrder(card.DealtAtPhase);

		if (phaseOrder == 1) // ThirdStreet
		{
			// ThirdStreet has 2 hole cards then 1 board card.
			// Use Location to ensure holes come before board, then DealOrder for hole ordering.
			if (card.Location == CardLocation.Hole)
			{
				// Hole cards: base 1000 + DealOrder gives 1001, 1002
				return 1000 + card.DealOrder;
			}
			else
			{
				// Board card: base 1100 + DealOrder ensures it sorts after all holes
				return 1100 + card.DealOrder;
			}
		}

		// For FourthStreet-SeventhStreet, use phase * 1000 + DealOrder
		// This maintains phase ordering and uses DealOrder as tiebreaker
		return phaseOrder * 1000 + card.DealOrder;
	}

	/// <summary>
	/// Orders cards in the correct deal sequence, handling Seven Card Stud's multi-street dealing.
	/// For stud games, uses a composite key based on phase and location; for other games, falls back to DealOrder.
	/// </summary>
	/// <param name="cards">The collection of cards to order.</param>
	/// <param name="isSevenCardStud">Whether this is a Seven Card Stud game.</param>
	/// <returns>Cards ordered in the correct deal sequence.</returns>
	private static IOrderedEnumerable<GameCard> OrderCardsForDisplay(IEnumerable<GameCard> cards, bool isSevenCardStud)
	{
		if (isSevenCardStud)
		{
			// Use composite order key that accounts for Location within ThirdStreet
			// to handle cases where DealOrder values might be incorrect.
			return cards.OrderBy(GetSevenCardStudOrderKey);
		}

		// For other games: Order by DealOrder which should be sequential per player
		return cards.OrderBy(c => c.DealOrder);
	}

	private static List<int> GetCardIndexes(List<Card> allCards, IEnumerable<Card> targetCards)
	{
		var indexes = new List<int>();
		var usedIndexes = new HashSet<int>();

		foreach (var target in targetCards)
		{
			for (var i = 0; i < allCards.Count; i++)
			{
				if (usedIndexes.Contains(i)) continue;

				if (allCards[i].Equals(target))
				{
					indexes.Add(i);
					usedIndexes.Add(i);
					break;
				}
			}
		}
		return indexes;
	}
}
