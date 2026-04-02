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
using CardGames.Poker.Api.Services.InMemoryEngine;
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

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Builds table state snapshots for SignalR broadcasts.
/// </summary>
public sealed class TableStateBuilder : ITableStateBuilder
{
	private static readonly StringComparer GameCodeComparer = StringComparer.OrdinalIgnoreCase;
	private static readonly Dictionary<string, Func<List<Card>, HandBase>> DrawHandFactories =
		new(GameCodeComparer)
		{
			[PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode] = cards => new TwosJacksManWithTheAxeDrawHand(cards),
			[PokerGameMetadataRegistry.KingsAndLowsCode] = cards => new KingsAndLowsDrawHand(cards)
		};
	private static readonly Dictionary<string, Func<List<GameCard>, List<Card>, string>> StudVariantEvaluators =
		new(GameCodeComparer)
		{
			[PokerGameMetadataRegistry.BaseballCode] = EvaluateBaseballHandDescription,
			[PokerGameMetadataRegistry.RazzCode] = EvaluateRazzHandDescription
		};
	private static readonly HashSet<string> StudGameCodes =
		new(GameCodeComparer)
		{
			PokerGameMetadataRegistry.SevenCardStudCode,
			PokerGameMetadataRegistry.RazzCode,
			PokerGameMetadataRegistry.BaseballCode,
			PokerGameMetadataRegistry.FollowTheQueenCode,
			PokerGameMetadataRegistry.PairPressureCode,
			PokerGameMetadataRegistry.TollboothCode
		};
	private static readonly Dictionary<string, string[]> TableSoundboardFiles =
		new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			["winning"] = ["pay_dat_man_his_money.mp3"]
		};
	private const int WinningSoundFrequencyHands = 10;

	private readonly CardsDbContext _context;
	private readonly IActionTimerService _actionTimerService;
	private readonly IPlayerChipWalletService _walletService;
	private readonly ILogger<TableStateBuilder> _logger;

	private sealed record UserProfile(string UserId, string? FirstName, string? AvatarUrl);
	private sealed record KingsAndLowsDeckOutcome(bool PlayerWins, ShowdownPlayerResultDto DeckResult, List<string>? Losers);

	/// <summary>
	/// Initializes a new instance of the <see cref="TableStateBuilder"/> class.
	/// </summary>
	public TableStateBuilder(CardsDbContext context, IActionTimerService actionTimerService, IPlayerChipWalletService walletService, ILogger<TableStateBuilder> logger)
	{
		_context = context;
		_actionTimerService = actionTimerService;
		_walletService = walletService;
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

		return await BuildPublicStateCoreAsync(game, gamePlayers, cancellationToken);
	}

	/// <summary>
	/// Core implementation for building public state from pre-loaded game data.
	/// </summary>
	private async Task<TableStatePublicDto> BuildPublicStateCoreAsync(
		Game game, List<GamePlayer> gamePlayers, CancellationToken cancellationToken)
	{
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
							OrderKey = StudOrderHelper.GetPlayerCardOrderKey(c.DealtAtPhase, c.Location == CardLocation.Hole, c.DealOrder),
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
				.Select(u => new { u.Id, Email = u.Email!, u.FirstName, u.AvatarUrl })
				.ToDictionaryAsync(
					u => u.Email,
					u => new UserProfile(u.Id, u.FirstName, u.AvatarUrl),
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
		var handHistory = await GetHandHistoryEntriesAsync(game.Id, currentUserPlayerId: null, take: 25, cancellationToken);

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
		var isBobBarkerShowdownReveal = IsBobBarkerGame(game.GameType?.Code)
			&& (string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(game.CurrentPhase, "Complete", StringComparison.OrdinalIgnoreCase));

		// Build community cards for variants that use table cards (e.g., Good Bad Ugly)
		var communityCards = await _context.GameCards
			.Where(c => c.GameId == game.Id
				&& !c.IsDiscarded
				&& c.HandNumber == game.CurrentHandNumber
				&& c.Location == CardLocation.Community)
			.OrderBy(c => c.DealOrder)
			.AsNoTracking()
			.Select(c => new CardPublicDto
			{
				IsFaceUp = c.IsVisible || isBobBarkerShowdownReveal,
				Rank = c.IsVisible || isBobBarkerShowdownReveal ? MapSymbolToRank(c.Symbol) : null,
				Suit = c.IsVisible || isBobBarkerShowdownReveal ? GetCardSuitString(c.Suit) : null,
				DealOrder = c.DealOrder,
				IsKlondikeCard = c.DealtAtPhase == "KlondikeCard"
			})
			.ToListAsync(cancellationToken);

		// Get action timer state
		var actionTimerState = _actionTimerService.GetTimerState(game.Id);
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

		var showdown = await BuildShowdownPublicDtoAsync(game, gamePlayers, userProfilesByEmail, cancellationToken);
		var soundEffects = BuildTableSoundEffects(game, showdown);

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
			Showdown = showdown,
			HandCompletedAtUtc = game.HandCompletedAt,
			NextHandStartsAtUtc = game.NextHandStartsAt,
			IsResultsPhase = isResultsPhase,
			SecondsUntilNextHand = secondsUntilNextHand,
			HandHistory = handHistory,
			SoundEffects = soundEffects,
			CurrentPhaseCategory = currentPhaseDescriptor?.Category,
			CurrentPhaseRequiresAction = currentPhaseDescriptor?.RequiresPlayerAction ?? false,
			CurrentPhaseAvailableActions = currentPhaseDescriptor?.AvailableActions,
			DrawingConfig = BuildDrawingConfigDto(rules),
				SpecialRules = await BuildSpecialRulesDtoAsync(rules, game, cancellationToken),
				PlayerVsDeck = playerVsDeck,
			ActionTimer = actionTimer,
			AllInRunout = allInRunout,
			ChipCheckPause = chipCheckPause,
			CommunityCards = communityCards.Count > 0 ? communityCards : null,
			IsDealersChoice = game.IsDealersChoice,
			RequiresJoinApproval = game.RequiresJoinApproval,
			DealersChoiceDealerPosition = game.IsDealersChoice ? game.DealersChoiceDealerPosition : null
		};
	}

	private static IReadOnlyList<TableSoundEffectDto>? BuildTableSoundEffects(Entities.Game game, ShowdownPublicDto? showdown)
	{
		if (game.CurrentHandNumber <= 0 || game.CurrentHandNumber % WinningSoundFrequencyHands != 0 || showdown is not { IsComplete: true })
		{
			return null;
		}

		var hasWinner = showdown.PlayerResults.Any(result => result.IsWinner || result.AmountWon > 0);
		if (!hasWinner)
		{
			return null;
		}

		var source = ChooseDeterministicSoundboardSource(game.Id, game.CurrentHandNumber, "winning");
		if (string.IsNullOrWhiteSpace(source))
		{
			return null;
		}

		return
		[
			new TableSoundEffectDto
			{
				CueKey = $"winning:{game.CurrentHandNumber}:{Path.GetFileName(source)}",
				EventKey = "winning",
				HandNumber = game.CurrentHandNumber,
				Source = source
			}
		];
	}

	private static string? ChooseDeterministicSoundboardSource(Guid gameId, int handNumber, string eventKey)
	{
		if (!TableSoundboardFiles.TryGetValue(eventKey, out var files) || files.Length == 0)
		{
			return null;
		}

		var seedBytes = Encoding.UTF8.GetBytes($"{gameId:N}:{handNumber}:{eventKey}");
		var hashBytes = SHA256.HashData(seedBytes);
		var selectedIndex = BitConverter.ToUInt32(hashBytes, 0) % (uint)files.Length;
		var fileName = files[(int)selectedIndex];
		return $"/sounds/soundboard/{eventKey}/{Uri.EscapeDataString(fileName)}";
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

		var cashierBalance = await _walletService.GetBalanceAsync(gamePlayer.PlayerId, cancellationToken);
		return await BuildPrivateStateCoreAsync(game, gamePlayer, cashierBalance, cancellationToken);
	}

	/// <summary>
	/// Core implementation for building private state from pre-loaded game data.
	/// Accepts the wallet balance instead of querying <see cref="IPlayerChipWalletService"/>
	/// so that <see cref="BuildFullStateAsync"/> can batch-load balances.
	/// </summary>
	private async Task<PrivateStateDto?> BuildPrivateStateCoreAsync(
		Game game, GamePlayer gamePlayer, int cashierBalance, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"BuildPrivateStateCoreAsync: Building state for player {PlayerName} at seat {SeatPosition} with {CardCount} cards (hand #{HandNumber})",
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
				.Where(c => c.GameId == game.Id
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
							.Where(c => c.GameId == game.Id
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
				else if (IsHoldTheBaseballGame(game.GameType?.Code) && playerCards.Count == 2)
				{
					var holdTheBaseballHand = new HoldTheBaseballHand(playerCards, communityCards);
					handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(holdTheBaseballHand);
				}
				// Klondike: Before reveal, treat the unknown Klondike Card as one wild.
				// After reveal, treat the Klondike Card and all cards of the same rank as wild.
				else if (IsGameType(game.GameType?.Code, PokerGameMetadataRegistry.KlondikeCode) && playerCards.Count == 2)
				{
					var klondikeCardEntity = await _context.GameCards
						.Where(c => c.GameId == game.Id
							&& c.HandNumber == game.CurrentHandNumber
							&& c.DealtAtPhase == "KlondikeCard")
						.AsNoTracking()
						.FirstOrDefaultAsync(cancellationToken);

					if (klondikeCardEntity is { IsVisible: true })
					{
						// Post-reveal: Klondike Card + all same-rank cards are wild
						var klondikeCard = new Card((Suit)klondikeCardEntity.Suit, (Symbol)klondikeCardEntity.Symbol);
						var klondikeHand = new KlondikeHand(playerCards, communityCards, klondikeCard);
						handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(klondikeHand);
					}
					else
					{
						// Pre-reveal: one unknown wild card
						var klondikeHand = new KlondikeHand(playerCards, communityCards);
						handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(klondikeHand);
					}
				}
				// Hold'em / Short-deck Hold'em style: 2 hole + up to 5 community
				else if (playerCards.Count == 2)
				{
					var holdemHand = new CardGames.Poker.Hands.CommunityCardHands.HoldemHand(playerCards, communityCards);
					handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(holdemHand);
				}
				// Irish Hold'em variants (including Phil's Mom) can have 3 hole cards after first discard.
				// Evaluate with community cards during this transitional state as well.
				else if (playerCards.Count == 3 && isCommunityCardGame)
				{
					var holdemHand = new CardGames.Poker.Hands.CommunityCardHands.HoldemHand(playerCards, communityCards);
					handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(holdemHand);
				}
				// Bob Barker: 4 active hole cards + up to 5 community, must use exactly 2 hole + 3 community.
				else if (playerCards.Count == 4 && IsBobBarkerGame(game.GameType?.Code))
				{
					var bobBarkerHand = new BobBarkerHand(playerCards, communityCards);
					handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(bobBarkerHand);
				}
				// Omaha style: 4 hole + up to 5 community
				else if (playerCards.Count == 4 && IsOmahaGame(game.GameType?.Code))
				{
					var omahaHand = new CardGames.Poker.Hands.CommunityCardHands.OmahaHand(playerCards, communityCards);
					handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(omahaHand);
				}
				// Nebraska style: 5 hole + up to 5 community, must use exactly 3 hole + 2 community
				else if (playerCards.Count == 5 && (IsNebraskaGame(game.GameType?.Code) || IsSouthDakotaGame(game.GameType?.Code)))
				{
					var nebraskaHand = new NebraskaHand(playerCards, communityCards);
					handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(nebraskaHand);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogDebug(ex, "Failed to compute hand evaluation description for game {GameId}, player {PlayerName}", game.Id, gamePlayer.Player.Name);
		}

		var isMyTurn = game.CurrentPlayerIndex == gamePlayer.SeatPosition;
		var availableActions = isMyTurn
			? await BuildAvailableActionsAsync(game.Id, game, gamePlayer, cancellationToken)
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
		var handHistory = await GetHandHistoryEntriesAsync(game.Id, gamePlayer.PlayerId, take: 25, cancellationToken);

		// Build chip history from hand history, including cashier balance (passed in for batch support)
		var chipHistory = BuildChipHistory(gamePlayer, handHistory, cashierBalance);

		return new PrivateStateDto
		{
			GameId = game.Id,
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

	/// <summary>
	/// Resolves SignalR user identifiers from pre-loaded game players without querying the database.
	/// Uses the same identity resolution logic as <see cref="GetPlayerUserIdsAsync"/>.
	/// </summary>
	private static IReadOnlyList<string> ResolvePlayerUserIds(IEnumerable<GamePlayer> gamePlayers)
	{
		return gamePlayers
			.Select(gp =>
			{
				var email = gp.Player.Email;
				var name = gp.Player.Name;
				var isMalformedEmail = !string.IsNullOrWhiteSpace(email) && email.Count(c => c == '@') > 1;
				if (isMalformedEmail && !string.IsNullOrWhiteSpace(name))
				{
					return name;
				}
				return email ?? name ?? gp.Player.ExternalId;
			})
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Cast<string>()
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	/// <summary>
	/// Finds the <see cref="GamePlayer"/> whose associated <see cref="Player"/> matches the given
	/// SignalR user identifier (email, name, or external ID — case-insensitive).
	/// </summary>
	private static GamePlayer? FindGamePlayerByUserId(IEnumerable<GamePlayer> gamePlayers, string userId)
	{
		return gamePlayers.FirstOrDefault(gp =>
			string.Equals(gp.Player.Email, userId, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(gp.Player.Name, userId, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(gp.Player.ExternalId, userId, StringComparison.OrdinalIgnoreCase));
	}

	/// <inheritdoc />
	public async Task<BroadcastStateBuildResult?> BuildFullStateAsync(Guid gameId, CancellationToken cancellationToken = default)
	{
		// ── 1. Single aggregate query ──────────────────────────────────────
		// Load Game + GameType + Pots + GamePlayers + Player + Cards in one
		// round-trip using AsSplitQuery to avoid Cartesian explosion.
		var game = await _context.Games
			.Include(g => g.GameType)
			.Include(g => g.Pots)
			.Include(g => g.GamePlayers.Where(gp => gp.Status != Entities.GamePlayerStatus.Left))
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GamePlayers.Where(gp => gp.Status != Entities.GamePlayerStatus.Left))
				.ThenInclude(gp => gp.Cards)
			.AsSplitQuery()
			.AsNoTracking()
			.FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

		if (game is null)
		{
			_logger.LogWarning("BuildFullStateAsync: Game {GameId} not found", gameId);
			return null;
		}

		var gamePlayers = game.GamePlayers
			.Where(gp => gp.Status != Entities.GamePlayerStatus.Left)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		// ── 2. Batch wallet balances ───────────────────────────────────────
		var playerIds = gamePlayers.Select(gp => gp.PlayerId).Distinct().ToArray();
		var walletBalances = await _context.PlayerChipAccounts
			.Where(a => playerIds.Contains(a.PlayerId))
			.AsNoTracking()
			.ToDictionaryAsync(a => a.PlayerId, a => a.Balance, cancellationToken);

		// ── 3. Build public state ──────────────────────────────────────────
		var publicState = await BuildPublicStateCoreAsync(game, gamePlayers, cancellationToken);

		// ── 4. Resolve user IDs ────────────────────────────────────────────
		var playerUserIds = ResolvePlayerUserIds(gamePlayers);

		// ── 5. Build all private states ────────────────────────────────────
		var privateStates = new Dictionary<string, PrivateStateDto>(StringComparer.OrdinalIgnoreCase);
		foreach (var userId in playerUserIds)
		{
			var gamePlayer = FindGamePlayerByUserId(gamePlayers, userId);
			if (gamePlayer is null)
			{
				_logger.LogWarning(
					"BuildFullStateAsync: No player found for resolved userId {UserId} in game {GameId}",
					userId, gameId);
				continue;
			}

			var balance = walletBalances.GetValueOrDefault(gamePlayer.PlayerId, 0);
			var privateState = await BuildPrivateStateCoreAsync(game, gamePlayer, balance, cancellationToken);
			if (privateState is not null)
			{
				privateStates[userId] = privateState;
			}
		}

		// ── 6. Compute cache version from SQL rowversion ───────────────────
		var versionNumber = BitConverter.ToUInt64(game.RowVersion.Reverse().ToArray(), 0);

		return new BroadcastStateBuildResult(
			publicState,
			privateStates,
			playerUserIds,
			versionNumber,
			game.CurrentHandNumber,
			game.CurrentPhase);
	}

	/// <inheritdoc />
	public async Task<BroadcastStateBuildResult?> BuildFullStateAsync(ActiveGameRuntimeState state, CancellationToken cancellationToken = default)
	{
		// ── 1. Adapt runtime state to entity form ──────────────────────────
		// This avoids the expensive aggregate Include/SplitQuery load in the DB-based overload.
		// Secondary queries (community cards, user profiles, hand history, etc.) inside the
		// core builder methods still hit the database using the checkpoint data.
		var (game, gamePlayers) = await AdaptRuntimeStateAsync(state, cancellationToken);

		// ── 2. Batch wallet balances (still requires DB) ───────────────────
		var playerIds = gamePlayers.Select(gp => gp.PlayerId).Distinct().ToArray();
		var walletBalances = await _context.PlayerChipAccounts
			.Where(a => playerIds.Contains(a.PlayerId))
			.AsNoTracking()
			.ToDictionaryAsync(a => a.PlayerId, a => a.Balance, cancellationToken);

		// ── 3. Build public state using adapted entities ───────────────────
		var publicState = await BuildPublicStateCoreAsync(game, gamePlayers, cancellationToken);

		// ── 4. Resolve user IDs ────────────────────────────────────────────
		var playerUserIds = ResolvePlayerUserIds(gamePlayers);

		// ── 5. Build all private states ────────────────────────────────────
		var privateStates = new Dictionary<string, PrivateStateDto>(StringComparer.OrdinalIgnoreCase);
		foreach (var userId in playerUserIds)
		{
			var gamePlayer = FindGamePlayerByUserId(gamePlayers, userId);
			if (gamePlayer is null)
			{
				_logger.LogWarning(
					"BuildFullStateAsync(runtime): No player found for resolved userId {UserId} in game {GameId}",
					userId, state.GameId);
				continue;
			}

			var balance = walletBalances.GetValueOrDefault(gamePlayer.PlayerId, 0);
			var privateState = await BuildPrivateStateCoreAsync(game, gamePlayer, balance, cancellationToken);
			if (privateState is not null)
			{
				privateStates[userId] = privateState;
			}
		}

		// ── 6. Use monotonic runtime version directly ──────────────────────
		return new BroadcastStateBuildResult(
			publicState,
			privateStates,
			playerUserIds,
			(ulong)state.Version,
			state.CurrentHandNumber,
			state.CurrentPhase);
	}

	/// <summary>
	/// Converts an <see cref="ActiveGameRuntimeState"/> to detached EF entity objects
	/// that the existing <see cref="BuildPublicStateCoreAsync"/> and
	/// <see cref="BuildPrivateStateCoreAsync"/> methods can consume, avoiding the
	/// expensive aggregate Include/SplitQuery database load.
	/// </summary>
	private async Task<(Game game, List<GamePlayer> gamePlayers)> AdaptRuntimeStateAsync(
		ActiveGameRuntimeState state, CancellationToken cancellationToken)
	{
		// Look up GameType once — it's a small, static reference table
		var gameType = state.GameTypeId.HasValue
			? await _context.GameTypes.AsNoTracking()
				.FirstOrDefaultAsync(gt => gt.Id == state.GameTypeId.Value, cancellationToken)
			: null;

		var game = new Game
		{
			Id = state.GameId,
			GameTypeId = state.GameTypeId,
			GameType = gameType,
			Name = state.Name,
			CurrentPhase = state.CurrentPhase,
			CurrentHandNumber = state.CurrentHandNumber,
			Status = state.Status,
			DealerPosition = state.DealerPosition,
			CurrentPlayerIndex = state.CurrentPlayerIndex,
			BringInPlayerIndex = state.BringInPlayerIndex,
			CurrentDrawPlayerIndex = state.CurrentDrawPlayerIndex,
			Ante = state.Ante,
			SmallBlind = state.SmallBlind,
			BigBlind = state.BigBlind,
			BringIn = state.BringIn,
			SmallBet = state.SmallBet,
			BigBet = state.BigBet,
			MinBet = state.MinBet,
			MaxBuyIn = state.MaxBuyIn,
			RequiresJoinApproval = state.RequiresJoinApproval,
			GameSettings = state.GameSettings,
			IsDealersChoice = state.IsDealersChoice,
			AreOddsVisibleToAllPlayers = state.AreOddsVisibleToAllPlayers,
			CurrentHandGameTypeCode = state.CurrentHandGameTypeCode,
			DealersChoiceDealerPosition = state.DealersChoiceDealerPosition,
			OriginalDealersChoiceDealerPosition = state.OriginalDealersChoiceDealerPosition,
			IsPausedForChipCheck = state.IsPausedForChipCheck,
			ChipCheckPauseStartedAt = state.ChipCheckPauseStartedAt,
			ChipCheckPauseEndsAt = state.ChipCheckPauseEndsAt,
			CreatedAt = state.CreatedAt,
			UpdatedAt = state.UpdatedAt,
			StartedAt = state.StartedAt,
			EndedAt = state.EndedAt,
			HandCompletedAt = state.HandCompletedAt,
			NextHandStartsAt = state.NextHandStartsAt,
			DrawCompletedAt = state.DrawCompletedAt,
			RandomSeed = state.RandomSeed,
			CreatedById = state.CreatedById,
			CreatedByName = state.CreatedByName,
			UpdatedById = state.UpdatedById,
			UpdatedByName = state.UpdatedByName,
			RowVersion = state.LastCheckpointRowVersion
		};

		// Build a lookup from runtime player ID → their cards
		var cardsByPlayerId = state.Cards
			.Where(c => c.GamePlayerId.HasValue)
			.GroupBy(c => c.GamePlayerId!.Value)
			.ToDictionary(g => g.Key, g => g.ToList());

		var gamePlayers = state.Players
			.Where(p => p.Status != GamePlayerStatus.Left)
			.OrderBy(p => p.SeatPosition)
			.Select(p =>
			{
				var gp = new GamePlayer
				{
					Id = p.Id,
					GameId = state.GameId,
					PlayerId = p.PlayerId,
					SeatPosition = p.SeatPosition,
					ChipStack = p.ChipStack,
					StartingChips = p.StartingChips,
					CurrentBet = p.CurrentBet,
					TotalContributedThisHand = p.TotalContributedThisHand,
					HasFolded = p.HasFolded,
					IsAllIn = p.IsAllIn,
					IsConnected = p.IsConnected,
					IsSittingOut = p.IsSittingOut,
					DropOrStayDecision = p.DropOrStayDecision,
					AutoDropOnDropOrStay = p.AutoDropOnDropOrStay,
					HasDrawnThisRound = p.HasDrawnThisRound,
					JoinedAtHandNumber = p.JoinedAtHandNumber,
					LeftAtHandNumber = p.LeftAtHandNumber,
					FinalChipCount = p.FinalChipCount,
					PendingChipsToAdd = p.PendingChipsToAdd,
					BringInAmount = p.BringInAmount,
					Status = p.Status,
					VariantState = p.VariantState,
					JoinedAt = p.JoinedAt,
					LeftAt = p.LeftAt,
					RowVersion = p.LastCheckpointRowVersion
				};

				// Attach a minimal Player entity for identity resolution
				gp.Player = new Player
				{
					Id = p.PlayerId,
					Name = p.PlayerName,
					Email = p.PlayerEmail,
					ExternalId = p.ExternalId,
					AvatarUrl = p.AvatarUrl
				};

				// Attach cards from runtime state
				gp.Cards = cardsByPlayerId.TryGetValue(p.Id, out var runtimeCards)
					? runtimeCards.Select(AdaptRuntimeCard).ToList()
					: [];

				return gp;
			})
			.ToList();

		// Set Pots on game entity (used by some code paths that access game.Pots)
		game.Pots = state.Pots.Select(p => new Entities.Pot
		{
			Id = p.Id,
			GameId = state.GameId,
			HandNumber = p.HandNumber,
			PotType = p.PotType,
			PotOrder = p.PotOrder,
			Amount = p.Amount,
			MaxContributionPerPlayer = p.MaxContributionPerPlayer,
			IsAwarded = p.IsAwarded,
			AwardedAt = p.AwardedAt,
			WinnerPayouts = p.WinnerPayouts,
			WinReason = p.WinReason,
			CreatedAt = p.CreatedAt
		}).ToList();

		return (game, gamePlayers);
	}

	private static GameCard AdaptRuntimeCard(RuntimeCard c) => new()
	{
		Id = c.Id,
		GameId = c.GameId,
		GamePlayerId = c.GamePlayerId,
		HandNumber = c.HandNumber,
		Suit = c.Suit,
		Symbol = c.Symbol,
		Location = c.Location,
		DealOrder = c.DealOrder,
		DealtAtPhase = c.DealtAtPhase,
		IsVisible = c.IsVisible,
		IsWild = c.IsWild,
		IsDiscarded = c.IsDiscarded,
		DiscardedAtDrawRound = c.DiscardedAtDrawRound,
		IsDrawnCard = c.IsDrawnCard,
		DrawnAtRound = c.DrawnAtRound,
		IsBuyCard = c.IsBuyCard,
		DealtAt = c.DealtAt
	};

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
		var isScrewYourNeighbor = IsScrewYourNeighborGame(gameTypeCode);
		var isInBetween = IsInBetweenGame(gameTypeCode);

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
			var shouldShowCard =
				(isSevenCardStud && card.IsVisible)
				|| (isScrewYourNeighbor && card.IsVisible)
				|| (isInBetween && card.IsVisible)
				|| shouldShowCardsForShowdown;

			return new CardPublicDto
			{
				IsFaceUp = shouldShowCard,
				Rank = shouldShowCard ? MapSymbolToRank(card.Symbol) : null,
				Suit = shouldShowCard ? GetCardSuitString(card.Suit) : null,
				DealOrder = card.DealOrder
			};
		}).ToList();

		var handEvaluationDescription = GetPublicHandEvaluationDescription(gameTypeCode, gamePlayer.VariantState);

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
			Cards = publicCards,
			HandEvaluationDescription = handEvaluationDescription
		};
	}

	private static string? GetPublicHandEvaluationDescription(string? gameTypeCode, string? variantState)
	{
		if (!string.Equals(gameTypeCode, PokerGameMetadataRegistry.ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		return variantState?.Trim().ToUpperInvariant() switch
		{
			"SYN_KEPT" => "Kept",
			"SYN_TRADED" => "Traded",
			_ => null
		};
	}

	private static string? GetPlayerFirstName(GamePlayer gamePlayer, IReadOnlyDictionary<string, UserProfile> userProfilesByEmail)
	{
		// Try Identity profile lookup by email
		var email = gamePlayer.Player.Email;
		if (string.IsNullOrWhiteSpace(email) && gamePlayer.Player.Name?.Contains('@') == true)
		{
			email = gamePlayer.Player.Name;
		}

		if (!string.IsNullOrWhiteSpace(email)
			&& userProfilesByEmail.TryGetValue(email, out var profile)
			&& !string.IsNullOrWhiteSpace(profile.FirstName))
		{
			return profile.FirstName.Trim();
		}

		// Fallback: derive from email local-part (TitleCase first segment)
		var emailSource = !string.IsNullOrWhiteSpace(email) ? email : gamePlayer.Player.Name;
		var atIndex = emailSource?.IndexOf('@') ?? -1;
		if (atIndex > 0)
		{
			var localPart = emailSource![..atIndex];
			var segments = localPart.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
			if (segments.Length > 0)
			{
				return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(segments[0].ToLowerInvariant());
			}
		}

		return null;
	}

	private static string? GetPlayerAvatarUrl(GamePlayer gamePlayer, IReadOnlyDictionary<string, UserProfile> userProfilesByEmail)
	{
		var email = gamePlayer.Player.Email;
		if (string.IsNullOrWhiteSpace(email) && gamePlayer.Player.Name?.Contains('@') == true)
		{
			email = gamePlayer.Player.Name;
		}

		if (!string.IsNullOrWhiteSpace(email)
			&& userProfilesByEmail.TryGetValue(email, out var profile))
		{
			return BuildAvatarUrl(profile.UserId, profile.AvatarUrl);
		}

		return BuildAvatarUrl(userId: null, gamePlayer.Player.AvatarUrl);
	}

	private static string? BuildAvatarUrl(string? userId, string? avatarUrl)
	{
		if (string.IsNullOrWhiteSpace(avatarUrl))
		{
			return null;
		}

		if (string.IsNullOrWhiteSpace(userId))
		{
			return null;
		}

		return ProfileAvatarRoutes.BuildAvatarPath(userId);
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

	private async Task<ShowdownPublicDto?> BuildShowdownPublicDtoAsync(
		Game game,
		List<GamePlayer> gamePlayers,
		Dictionary<string, UserProfile> userProfilesByEmail,
		CancellationToken cancellationToken)
	{
		var isTwosJacksAxe = IsTwosJacksAxeGame(game.GameType?.Code);
		var isGoodBadUgly = IsGoodBadUglyGame(game.GameType?.Code);
		var isHoldEm = IsHoldEmGame(game.GameType?.Code);
		var isHoldTheBaseball = IsHoldTheBaseballGame(game.GameType?.Code);
		var isOmaha = IsOmahaGame(game.GameType?.Code);
		var isBobBarker = IsBobBarkerGame(game.GameType?.Code);
		var isNebraska = IsNebraskaGame(game.GameType?.Code);
		var isSouthDakota = IsSouthDakotaGame(game.GameType?.Code);
		var isIrishHoldEm = IsIrishHoldEmGame(game.GameType?.Code);

		var isSevenCardStud = IsGameType(game.GameType?.Code, PokerGameMetadataRegistry.SevenCardStudCode)
			|| IsGameType(game.GameType?.Code, PokerGameMetadataRegistry.RazzCode)
			|| IsGameType(game.GameType?.Code, PokerGameMetadataRegistry.TollboothCode);
		var isRazz = IsGameType(game.GameType?.Code, PokerGameMetadataRegistry.RazzCode);
		var isBaseball = IsBaseballGame(game.GameType?.Code);
		var isKingsAndLows = IsKingsAndLowsGame(game.GameType?.Code);
		var isFollowTheQueen = IsFollowTheQueenGame(game.GameType?.Code);
		var isPairPressure = IsPairPressureGame(game.GameType?.Code);
		var isScrewYourNeighbor = IsScrewYourNeighborGame(game.GameType?.Code);
		var isInBetween = IsInBetweenGame(game.GameType?.Code);
		var isStudStyleShowdown = isSevenCardStud || isBaseball || isFollowTheQueen || isPairPressure;
		var isTerminalScrewYourNeighborShowdown =
			isScrewYourNeighbor && string.Equals(game.CurrentPhase, "Ended", StringComparison.OrdinalIgnoreCase);

		// In-Between has no traditional showdown — skip evaluation entirely
		if (isInBetween)
		{
			return null;
		}

		if (game.CurrentPhase != "Showdown" &&
			game.CurrentPhase != "Complete" &&
			game.CurrentPhase != "PotMatching" &&
			!isTerminalScrewYourNeighborShowdown)
		{
			return null;
		}

		// Evaluate all hands for players who haven't folded
		// Use HandBase as the base type since all hand types inherit from it
		var playerHandEvaluations = new Dictionary<string, (HandBase hand, TwosJacksManWithTheAxeDrawHand? twosJacksHand, KingsAndLowsDrawHand? kingsAndLowsHand, SevenCardStudHand? studHand, GamePlayer gamePlayer, List<GameCard> cards, List<int> wildIndexes, List<int> bestCardIndexes)>();
		var showdownPlayers = gamePlayers
			.Where(gp => !gp.HasFolded)
			.ToList();

		// Good Bad Ugly: players have <=4 hole cards + community cards (The Good, The Bad, The Ugly)
		// Must be handled before the cards.Count >= 5 check since player-owned cards may be fewer than 5
		if (isGoodBadUgly)
		{
			var gbuCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			int? gbuWildRank = null;
			var goodCard = gbuCommunityCards.FirstOrDefault(c =>
				string.Equals(c.DealtAtPhase, "TheGood", StringComparison.OrdinalIgnoreCase) && c.IsVisible);
			if (goodCard is not null)
			{
				gbuWildRank = (int)goodCard.Symbol;
			}

			var gbuWildRules = new GoodBadUglyWildCardRules();
			var visibleCommunityCards = gbuCommunityCards
				.Where(c => c.IsVisible)
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var ownedCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();
				var allCoreCards = ownedCoreCards.Concat(visibleCommunityCards).ToList();
				var allDisplayCards = ownedCards.ToList();

				var gbuHand = new GoodBadUglyHand(allCoreCards, [], [], gbuWildRank, gbuWildRules);

				// Determine wild card indexes (within the player's owned cards only)
				var wildIndexes = new List<int>();
				if (gbuWildRank.HasValue)
				{
					for (int i = 0; i < ownedCoreCards.Count; i++)
					{
						if (ownedCoreCards[i].Value == gbuWildRank.Value)
						{
							wildIndexes.Add(i);
						}
					}
				}

				playerHandEvaluations[gp.Player.Name] = (gbuHand, null, null, null, gp, allDisplayCards, wildIndexes,
					GetCardIndexes(allCoreCards, gbuHand.EvaluatedBestCards));
			}
		}

		// Hold'Em: players have 2 hole cards + 5 shared community cards → best 5-of-7
		if (isHoldEm)
		{
			var holdemCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = holdemCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 2)
				{
					continue;
				}

				var holdemHand = new HoldemHand(holeCoreCards, communityCoreCards);

				// Build full card list for display: hole cards first, then community cards
				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(holdemCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();

				// Find best 5-card hand from the 7 cards for highlighting
				var bestFive = FindBestFiveCardHand(allCoreCards);

				playerHandEvaluations[gp.Player.Name] = (holdemHand, null, null, null, gp, allDisplayCards, [],
					GetCardIndexes(allCoreCards, bestFive));
			}
		}

		// Hold the Baseball: Hold'em structure with 3s and 9s wild in hole and community cards.
		if (isHoldTheBaseball)
		{
			var holdTheBaseballCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = holdTheBaseballCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 2)
				{
					continue;
				}

				var holdTheBaseballHand = new HoldTheBaseballHand(holeCoreCards, communityCoreCards);

				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(holdTheBaseballCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();

				var wildIndexes = new List<int>();
				for (var i = 0; i < allCoreCards.Count; i++)
				{
					if (holdTheBaseballHand.WildCards.Contains(allCoreCards[i]))
					{
						wildIndexes.Add(i);
					}
				}

				playerHandEvaluations[gp.Player.Name] = (holdTheBaseballHand, null, null, null, gp, allDisplayCards, wildIndexes,
					GetCardIndexes(allCoreCards, holdTheBaseballHand.BestHandSourceCards));
			}
		}

		// Omaha: players have 4 hole cards + 5 shared community cards → best 5 using exactly 2 hole + 3 community
		if (isOmaha)
		{
			var omahaCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = omahaCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 4)
				{
					continue;
				}

				var omahaHand = new OmahaHand(holeCoreCards, communityCoreCards);

				// Build full card list for display: hole cards first, then community cards
				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(omahaCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();

				// Find best 5-card hand using Omaha rules: exactly 2 hole + 3 community
				var bestFive = FindBestOmahaHand(holeCoreCards, communityCoreCards);

				playerHandEvaluations[gp.Player.Name] = (omahaHand, null, null, null, gp, allDisplayCards, [],
					GetCardIndexes(allCoreCards, bestFive));
			}
		}

		if (isBobBarker)
		{
			var bobBarkerCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded
					&& c.IsVisible)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = bobBarkerCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var selectedShowcaseDealOrder = BobBarkerVariantState.GetSelectedShowcaseDealOrder(gp);
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded
						&& c.HandNumber == game.CurrentHandNumber
						&& (selectedShowcaseDealOrder is null || c.DealOrder != selectedShowcaseDealOrder.Value))
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 4)
				{
					continue;
				}

				var bobBarkerHand = new BobBarkerHand(holeCoreCards, communityCoreCards);
				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(bobBarkerCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();
				var bestFive = FindBestOmahaHand(holeCoreCards, communityCoreCards);

				playerHandEvaluations[gp.Player.Name] = (bobBarkerHand, null, null, null, gp, allDisplayCards, [],
					GetCardIndexes(allCoreCards, bestFive));
			}
		}

		// Nebraska: players have 5 hole cards + 5 shared community cards → best 5 using exactly 3 hole + 2 community
		if (isNebraska || isSouthDakota)
		{
			var nebraskaCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = nebraskaCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 5)
				{
					continue;
				}

				var nebraskaHand = new NebraskaHand(holeCoreCards, communityCoreCards);

				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(nebraskaCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();

				var bestFive = FindBestNebraskaHand(holeCoreCards, communityCoreCards);

				playerHandEvaluations[gp.Player.Name] = (nebraskaHand, null, null, null, gp, allDisplayCards, [],
					GetCardIndexes(allCoreCards, bestFive));
			}
		}

		// Irish Hold'Em: post-discard players have 2 hole cards + 5 community → best 5-of-7 (same as Hold'Em)
		if (isIrishHoldEm)
		{
			var irishCommunityCards = await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var communityCoreCards = irishCommunityCards
				.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
				.ToList();

			foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
			{
				var ownedCards = gp.Cards
					.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
					.OrderBy(c => c.DealOrder)
					.ToList();

				var holeCoreCards = ownedCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();

				if (holeCoreCards.Count < 2)
				{
					continue;
				}

				var holdemHand = new HoldemHand(holeCoreCards, communityCoreCards);

				// Build full card list for display: hole cards first, then community cards
				var allDisplayCards = ownedCards.ToList();
				allDisplayCards.AddRange(irishCommunityCards);
				var allCoreCards = holeCoreCards.Concat(communityCoreCards).ToList();

				// Find best 5-card hand from the 7 cards for highlighting
				var bestFive = FindBestFiveCardHand(allCoreCards);

				playerHandEvaluations[gp.Player.Name] = (holdemHand, null, null, null, gp, allDisplayCards, [],
					GetCardIndexes(allCoreCards, bestFive));
			}
		}

		foreach (var gp in gamePlayers.Where(p => !p.HasFolded))
		{
			// Skip if already evaluated (e.g., by GBU-specific handling above)
			if (playerHandEvaluations.ContainsKey(gp.Player.Name))
			{
				continue;
			}

			var filteredCards = gp.Cards
				.Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber);
			var cards = OrderCardsForDisplay(filteredCards, isStudStyleShowdown).ToList();

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
				else if (isPairPressure)
				{
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
					var faceUpCardsInOrder = await GetOrderedFaceUpCardsAsync(game, cancellationToken);

					if (initialHoleCards.Count >= 2)
					{
						var downCard = holeCards.Count >= 3
							? new Card((Suit)holeCards[2].Suit, (Symbol)holeCards[2].Symbol)
							: null;
						var pairPressureHand = new PairPressureHand(initialHoleCards, openCards, downCard, faceUpCardsInOrder);
						var wildCards = pairPressureHand.WildCards;
						var wildIndexes = new List<int>();
						for (var i = 0; i < coreCards.Count; i++)
						{
							if (wildCards.Contains(coreCards[i]))
							{
								wildIndexes.Add(i);
							}
						}

						playerHandEvaluations[gp.Player.Name] = (pairPressureHand, null, null, null, gp, cards, wildIndexes, GetCardIndexes(coreCards, pairPressureHand.BestHandSourceCards));
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
						if (isRazz)
						{
							var razzHand = new RazzHand(initialHoleCards, openCards, [downCard]);
							playerHandEvaluations[gp.Player.Name] = (razzHand, null, null, null, gp, cards, [], GetCardIndexes(coreCards, razzHand.GetBestLowHand()));
						}
						else
						{
							var studHand = new SevenCardStudHand(initialHoleCards, openCards, downCard);
							playerHandEvaluations[gp.Player.Name] = (studHand, null, null, studHand, gp, cards, [], GetCardIndexes(coreCards, studHand.GetBestHand()));
						}
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
		var actualPayouts = new Dictionary<string, (int Total, int Sevens, int High, int Showcase)>(StringComparer.OrdinalIgnoreCase);
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

						int showcaseAmount = 0;
						if (element.TryGetProperty("showcaseAmount", out var showcaseProp))
						{
							showcaseAmount = showcaseProp.GetInt32();
						}

						if (actualPayouts.TryGetValue(name, out var existing))
						{
							actualPayouts[name] = (existing.Total + amount, existing.Sevens + sevensAmount, existing.High + highAmount, existing.Showcase + showcaseAmount);
						}
						else
						{
							actualPayouts[name] = (amount, sevensAmount, highAmount, showcaseAmount);
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
			// For Good Bad Ugly, filter out players eliminated by The Ugly before determining winners
			if (isGoodBadUgly)
			{
				var eligibleEvaluations = playerHandEvaluations
					.Where(kvp => !string.Equals(kvp.Value.gamePlayer.VariantState, "UGLY_ELIMINATED", StringComparison.OrdinalIgnoreCase))
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

				if (eligibleEvaluations.Count > 0)
				{
					var maxEligibleStrength = eligibleEvaluations.Values.Max(h => h.hand.Strength);
					foreach (var kvp in eligibleEvaluations.Where(k => k.Value.hand.Strength == maxEligibleStrength))
					{
						highHandWinners.Add(kvp.Key);
					}
				}
				else
				{
					// All remaining players were eliminated by The Ugly: split among all
					foreach (var kvp in playerHandEvaluations)
					{
						highHandWinners.Add(kvp.Key);
					}
				}
			}
			else
			{
				// Determine high hand winners (highest hand strength)
				var maxStrength = playerHandEvaluations.Values.Max(h => h.hand.Strength);
				foreach (var kvp in playerHandEvaluations.Where(k => k.Value.hand.Strength == maxStrength))
				{
					highHandWinners.Add(kvp.Key);
				}
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
		else if (isScrewYourNeighbor)
		{
			// SYN: each player has exactly 1 card. Lowest card value loses (Ace=1, King=13).
			// Players who do NOT have the lowest value are winners.
			var synActivePlayers = showdownPlayers;

			var synHandCards = await _context.GameCards
				.Where(gc => gc.GameId == game.Id
					&& gc.HandNumber == game.CurrentHandNumber
					&& gc.GamePlayerId != null
					&& gc.Location == CardLocation.Hand
					&& !gc.IsDiscarded)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var synPlayerCards = new Dictionary<Guid, GameCard>();
			foreach (var card in synHandCards)
			{
				if (card.GamePlayerId.HasValue)
				{
					synPlayerCards[card.GamePlayerId.Value] = card;
				}
			}

			var synPlayerValues = new List<(GamePlayer Player, int CardValue)>();
			foreach (var player in synActivePlayers)
			{
				if (synPlayerCards.TryGetValue(player.Id, out var card))
				{
					var value = ScrewYourNeighborFlowHandler.GetScrewYourNeighborCardValue(card.Symbol);
					synPlayerValues.Add((player, value));
				}
			}

			if (synPlayerValues.Count > 0)
			{
				var lowestValue = synPlayerValues.Min(pv => pv.CardValue);
				foreach (var pv in synPlayerValues.Where(pv => pv.CardValue != lowestValue))
				{
					highHandWinners.Add(pv.Player.Player.Name);
				}
			}

			showdownPlayers = synPlayerValues
				.Select(pv => pv.Player)
				.ToList();
		}
		else if (gamePlayers.Count(gp => !gp.HasFolded) == 1)
		{
			// Only one player remaining (won by fold)
			var winner = gamePlayers.First(gp => !gp.HasFolded);
			highHandWinners.Add(winner.Player.Name);
		}

		// Combined winners for IsWinner flag
		var showcaseWinners = isBobBarker
			? actualPayouts.Where(kvp => kvp.Value.Showcase > 0).Select(kvp => kvp.Key).ToHashSet(StringComparer.OrdinalIgnoreCase)
			: new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var mainHandWinners = isBobBarker && actualPayouts.Any(kvp => kvp.Value.High > 0)
			? actualPayouts.Where(kvp => kvp.Value.High > 0).Select(kvp => kvp.Key).ToHashSet(StringComparer.OrdinalIgnoreCase)
			: highHandWinners;
		var allWinners = mainHandWinners.Union(sevensWinners).Union(showcaseWinners).ToHashSet(StringComparer.OrdinalIgnoreCase);

		var allLosers = isKingsAndLows
			? gamePlayers.Where(gp => !gp.HasFolded && !highHandWinners.Contains(gp.Player.Name))
				.Select(gp => gp.Player.Name)
				.ToList()
			: null;

		var bobBarkerDealerCard = isBobBarker
			? await _context.GameCards
				.Where(c => c.GameId == game.Id
					&& c.HandNumber == game.CurrentHandNumber
					&& c.Location == CardLocation.Community
					&& c.GamePlayerId == null
					&& !c.IsDiscarded)
				.OrderBy(c => c.DealOrder)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken)
			: null;

		// Build player results
		var playerResults = showdownPlayers
			.Select(gp =>
			{
				var isWinner = allWinners.Contains(gp.Player.Name);
				var isSevensWinner = sevensWinners.Contains(gp.Player.Name);
				var isHighHandWinner = mainHandWinners.Contains(gp.Player.Name);
				var isShowcaseWinner = showcaseWinners.Contains(gp.Player.Name);
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
				var selectedShowcaseDealOrder = isBobBarker
					? BobBarkerVariantState.GetSelectedShowcaseDealOrder(gp)
					: null;
				var showcaseCard = isBobBarker && selectedShowcaseDealOrder.HasValue
					? gp.Cards.FirstOrDefault(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber && c.DealOrder == selectedShowcaseDealOrder.Value)
					: null;

				return new ShowdownPlayerResultDto
				{
					PlayerName = gp.Player.Name,
					PlayerFirstName = GetPlayerFirstName(gp, userProfilesByEmail),
					SeatPosition = gp.SeatPosition,
					HandRanking = handRanking,
					HandDescription = isGoodBadUgly && string.Equals(gp.VariantState, "UGLY_ELIMINATED", StringComparison.OrdinalIgnoreCase)
						? "Dead Hand (The Ugly)"
						: playerHandEvaluations.TryGetValue(gp.Player.Name, out var e)
							? HandDescriptionFormatter.GetHandDescription(e.hand)
							: null,
					AmountWon = payouts.Total,
					SevensAmountWon = payouts.Sevens,
					HighHandAmountWon = payouts.High,
					ShowcaseAmountWon = payouts.Showcase,
					IsWinner = isWinner,
					IsSevensWinner = isSevensWinner,
					IsHighHandWinner = isHighHandWinner,
					IsShowcaseWinner = isShowcaseWinner,
					WildCardIndexes = wildIndexes,
					BestCardIndexes = bestCardIndexes,
					ShowcaseCard = showcaseCard is null
						? null
						: new CardPublicDto
						{
							IsFaceUp = true,
							Rank = MapSymbolToRank(showcaseCard.Symbol),
							Suit = GetCardSuitString(showcaseCard.Suit),
							DealOrder = showcaseCard.DealOrder
						},
					ShowcaseCardValue = showcaseCard is null
						? null
						: GetBobBarkerCardValue(showcaseCard.Symbol, bobBarkerDealerCard?.Symbol == Entities.CardSymbol.Ace),
					Cards = OrderCardsForDisplay(
							gp.Cards.Where(c => !c.IsDiscarded
								&& c.HandNumber == game.CurrentHandNumber
								&& (!isBobBarker || selectedShowcaseDealOrder is null || c.DealOrder != selectedShowcaseDealOrder.Value)),
							isStudStyleShowdown)
						.Select(c => new CardPublicDto
						{
							IsFaceUp = true,
							Rank = MapSymbolToRank(c.Symbol),
							Suit = GetCardSuitString(c.Suit),
							DealOrder = c.DealOrder
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
			var playerResult = playerResults[0];
			var playerStrength = playerHandEvaluations.Values.FirstOrDefault().hand?.Strength ?? 0;
			var deckOutcome = await BuildKingsAndLowsDeckOutcomeAsync(
				game,
				playerResult.PlayerName,
				playerStrength,
				cancellationToken);

			if (deckOutcome is not null)
			{
				if (deckOutcome.PlayerWins)
				{
					playerResults[0] = playerResult with { IsWinner = true };
					highHandWinners.Add(playerResult.PlayerName);
				}
				else
				{
					playerResults[0] = playerResult with { IsWinner = false };
					highHandWinners.Clear();
					allLosers = deckOutcome.Losers;
				}

				playerResults.Add(deckOutcome.DeckResult);
			}
		}

		return new ShowdownPublicDto
		{
			PlayerResults = playerResults,
			IsComplete = game.CurrentPhase == "Complete" || isTerminalScrewYourNeighborShowdown,
			SevensWinners = isTwosJacksAxe ? sevensWinners.ToList() : null,
			HighHandWinners = isTwosJacksAxe ? highHandWinners.ToList() : null,
			Losers = allLosers,
			SevensPoolRolledOver = sevensPoolRolledOver,
			BobBarker = isBobBarker && bobBarkerDealerCard is not null
				? new BobBarkerShowdownStateDto
				{
					DealerCard = new CardPublicDto
					{
						IsFaceUp = true,
						Rank = MapSymbolToRank(bobBarkerDealerCard.Symbol),
						Suit = GetCardSuitString(bobBarkerDealerCard.Suit),
						DealOrder = bobBarkerDealerCard.DealOrder
					},
					DealerCardValue = GetBobBarkerCardValue(bobBarkerDealerCard.Symbol, bobBarkerDealerCard.Symbol == Entities.CardSymbol.Ace),
					MainHandWinners = mainHandWinners.OrderBy(name => name).ToList(),
					ShowcaseWinners = showcaseWinners.OrderBy(name => name).ToList()
				}
				: null
		};
	}

	private static int GetBobBarkerCardValue(Entities.CardSymbol symbol, bool aceHigh)
	{
		if (symbol == Entities.CardSymbol.Ace)
		{
			return aceHigh ? 14 : 1;
		}

		return (int)symbol;
	}

	private async Task<int> CalculateTotalPotAsync(Game game, int handNumber, CancellationToken cancellationToken)
	{
		if (IsInBetweenGame(game.GameType?.Code))
		{
			// In-Between uses a single hand with a pot that changes each turn.
			return await _context.Pots
				.Where(p => p.GameId == game.Id && p.HandNumber == handNumber && !p.IsAwarded)
				.AsNoTracking()
				.SumAsync(p => p.Amount, cancellationToken);
		}

		if (IsScrewYourNeighborGame(game.GameType?.Code))
		{
			// SYN carries the funded pot across hands in the Pots table.
			// While showing results between hands, display the upcoming hand pot.
			var isWaitingForNextHand = game.CurrentPhase == "Complete";
			var targetHandNumber = isWaitingForNextHand ? handNumber + 1 : handNumber;

			var total = await _context.Pots
				.Where(p => p.GameId == game.Id && p.HandNumber == targetHandNumber && !p.IsAwarded)
				.AsNoTracking()
				.SumAsync(p => p.Amount, cancellationToken);

			if (total == 0 && isWaitingForNextHand)
			{
				total = await _context.Pots
					.Where(p => p.GameId == game.Id && p.HandNumber == handNumber && !p.IsAwarded)
					.AsNoTracking()
					.SumAsync(p => p.Amount, cancellationToken);
			}

			return total;
		}

		// For Kings and Lows, the pot is tracked in the Pots table, not TotalContributedThisHand
		if (IsKingsAndLowsGame(game.GameType?.Code))
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

	private static bool IsScrewYourNeighborGame(string? gameCode)
	{
		return string.Equals(gameCode, PokerGameMetadataRegistry.ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsInBetweenGame(string? gameCode)
	{
		return string.Equals(gameCode, PokerGameMetadataRegistry.InBetweenCode, StringComparison.OrdinalIgnoreCase);
	}

	private async Task<KingsAndLowsDeckOutcome?> BuildKingsAndLowsDeckOutcomeAsync(
		Game game,
		string playerName,
		long playerStrength,
		CancellationToken cancellationToken)
	{
		var deckCards = await _context.GameCards
			.Where(c => c.GameId == game.Id &&
						c.HandNumber == game.CurrentHandNumber &&
						!c.IsDiscarded &&
						c.GamePlayerId == null &&
						c.Location == CardLocation.Board)
			.OrderBy(c => c.DealOrder)
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		if (deckCards.Count < 5)
		{
			return null;
		}

		var deckCoreCards = deckCards.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol)).ToList();
		var deckHand = new KingsAndLowsDrawHand(deckCoreCards);

		var deckWildCards = deckHand.WildCards;
		var deckWildIndexes = new List<int>();
		for (var i = 0; i < deckCoreCards.Count; i++)
		{
			if (deckWildCards.Contains(deckCoreCards[i]))
			{
				deckWildIndexes.Add(i);
			}
		}

		var playerWins = playerStrength >= deckHand.Strength;
		var deckWins = !playerWins;

		var deckResult = new ShowdownPlayerResultDto
		{
			PlayerName = "The Deck",
			PlayerFirstName = "Deck",
			SeatPosition = -1,
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

		return new KingsAndLowsDeckOutcome(
			playerWins,
			deckResult,
			deckWins ? [playerName] : null);
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
		var isPairPressure = string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.PairPressureCode, StringComparison.OrdinalIgnoreCase);
		var isKlondike = string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.KlondikeCode, StringComparison.OrdinalIgnoreCase);
		var isGoodBadUgly = string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.GoodBadUglyCode, StringComparison.OrdinalIgnoreCase);

		// Dynamic-wild stud variants compute their active wild ranks from face-up cards.
		IReadOnlyList<string>? dynamicWildRanks = null;
		if (isFollowTheQueen)
		{
			dynamicWildRanks = await ComputeFollowTheQueenWildRanksAsync(game, cancellationToken);
		}
		else if (isPairPressure)
		{
			dynamicWildRanks = await ComputePairPressureWildRanksAsync(game, cancellationToken);
		}
		else if (isKlondike)
		{
			dynamicWildRanks = await ComputeKlondikeWildRanksAsync(game, cancellationToken);
		}
		else if (isGoodBadUgly)
		{
			dynamicWildRanks = await ComputeGoodBadUglyWildRanksAsync(game, cancellationToken);
		}

		return new GameSpecialRulesDto
		{
			HasDropOrStay = rules.SpecialRules.ContainsKey("DropOrStay"),
			HasKeepOrTrade = rules.SpecialRules.ContainsKey("KeepOrTrade"),
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

	private async Task<IReadOnlyList<string>> ComputePairPressureWildRanksAsync(
		Entities.Game game,
		CancellationToken cancellationToken)
	{
		var sortedFaceUpCards = await GetOrderedFaceUpCardsAsync(game, cancellationToken);
		var wildRanks = new PairPressureWildCardRules()
			.DetermineWildRanks(sortedFaceUpCards)
			.Select(rank => MapSymbolToRank((Entities.CardSymbol)rank))
			.Where(rank => rank is not null)
			.Cast<string>()
			.ToList();

		return wildRanks;
	}

	/// <summary>
	/// Computes the current wild card rank for Klondike.
	/// When the Klondike Card is revealed, all cards of that rank are wild.
	/// Returns empty if the card has not been revealed yet.
	/// </summary>
	private async Task<IReadOnlyList<string>> ComputeKlondikeWildRanksAsync(
		Entities.Game game,
		CancellationToken cancellationToken)
	{
		var klondikeCard = await _context.GameCards
			.Where(c => c.GameId == game.Id
				&& c.HandNumber == game.CurrentHandNumber
				&& c.DealtAtPhase == "KlondikeCard"
				&& c.IsVisible)
			.AsNoTracking()
			.FirstOrDefaultAsync(cancellationToken);

		if (klondikeCard is null)
		{
			return [];
		}

		var rank = MapSymbolToRank(klondikeCard.Symbol);
		return rank is not null ? [rank] : [];
	}

	/// <summary>
	/// Computes the current wild card rank for Good Bad Ugly.
	/// When The Good card is revealed, all cards of that rank are wild.
	/// Returns empty if The Good card has not been revealed yet.
	/// </summary>
	private async Task<IReadOnlyList<string>> ComputeGoodBadUglyWildRanksAsync(
		Entities.Game game,
		CancellationToken cancellationToken)
	{
		var goodCard = await _context.GameCards
			.Where(c => c.GameId == game.Id
				&& c.HandNumber == game.CurrentHandNumber
				&& c.DealtAtPhase == "TheGood"
				&& c.IsVisible)
			.AsNoTracking()
			.FirstOrDefaultAsync(cancellationToken);

		if (goodCard is null)
		{
			return [];
		}

		var rank = MapSymbolToRank(goodCard.Symbol);
		return rank is not null ? [rank] : [];
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
			HasKeepOrTrade = rules.SpecialRules.ContainsKey("KeepOrTrade"),
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
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.HoldTheBaseballCode, StringComparison.OrdinalIgnoreCase))
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
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.PairPressureCode, StringComparison.OrdinalIgnoreCase))
		{
			if (dynamicWildRanks is { Count: > 0 })
			{
				wildRanks.AddRange(dynamicWildRanks);
			}
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.KlondikeCode, StringComparison.OrdinalIgnoreCase))
		{
			if (dynamicWildRanks is { Count: > 0 })
			{
				wildRanks.AddRange(dynamicWildRanks);
			}
		}
		else if (string.Equals(rules.GameTypeCode, PokerGameMetadataRegistry.GoodBadUglyCode, StringComparison.OrdinalIgnoreCase))
		{
			if (dynamicWildRanks is { Count: > 0 })
			{
				wildRanks.AddRange(dynamicWildRanks);
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

		// Determine decision maker: any active non-staying player, preferring the dealer.
		// The decision maker chooses which cards to discard from the deck's hand.
		var dealerSeatPosition = game.DealerPosition;
		var orderedPlayers = gamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

		GamePlayer? decisionMaker = null;

		// Try dealer first (must be active, not sitting out, and not the staying player)
		var dealer = orderedPlayers.FirstOrDefault(gp => gp.SeatPosition == dealerSeatPosition);
		if (dealer is not null &&
			dealer.PlayerId != stayingPlayer.PlayerId &&
			dealer.Status == Entities.GamePlayerStatus.Active &&
			!dealer.IsSittingOut)
		{
			decisionMaker = dealer;
		}

		// If dealer can't be the decision maker, find the first eligible player
		// searching clockwise from the dealer position
		if (decisionMaker is null)
		{
			var dealerIndex = orderedPlayers.FindIndex(gp => gp.SeatPosition == dealerSeatPosition);
			if (dealerIndex < 0) dealerIndex = 0;

			for (int i = 1; i <= orderedPlayers.Count; i++)
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
		return !string.IsNullOrWhiteSpace(gameTypeCode) && StudGameCodes.Contains(gameTypeCode);
	}

	private static bool IsBaseballGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.BaseballCode);

	private static bool IsKingsAndLowsGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.KingsAndLowsCode);

	private static bool IsFollowTheQueenGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.FollowTheQueenCode);

	private static bool IsPairPressureGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.PairPressureCode);

	private static bool IsTwosJacksAxeGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode);

	private static bool IsGoodBadUglyGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.GoodBadUglyCode);

	private static bool IsHoldEmGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.HoldEmCode)
		   || IsGameType(gameTypeCode, PokerGameMetadataRegistry.RedRiverCode)
		   || IsGameType(gameTypeCode, PokerGameMetadataRegistry.KlondikeCode);

	private static bool IsHoldTheBaseballGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.HoldTheBaseballCode);

	private static bool IsOmahaGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.OmahaCode);

	private static bool IsNebraskaGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.NebraskaCode);

	private static bool IsSouthDakotaGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.SouthDakotaCode);

	private static bool IsIrishHoldEmGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.IrishHoldEmCode)
		   || IsGameType(gameTypeCode, PokerGameMetadataRegistry.PhilsMomCode)
		   || IsGameType(gameTypeCode, PokerGameMetadataRegistry.CrazyPineappleCode);

	private static bool IsBobBarkerGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.BobBarkerCode);

	private static bool IsGameType(string? gameTypeCode, string expectedCode)
		=> !string.IsNullOrWhiteSpace(gameTypeCode) &&
		   string.Equals(gameTypeCode, expectedCode, StringComparison.OrdinalIgnoreCase);

	private static HandBase BuildDrawHandForGame(string? gameTypeCode, List<Card> playerCards)
	{
		if (!string.IsNullOrWhiteSpace(gameTypeCode) && DrawHandFactories.TryGetValue(gameTypeCode, out var factory))
		{
			return factory(playerCards);
		}

		return new DrawHand(playerCards);
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

	private static string EvaluateBaseballHandDescription(List<GameCard> holeCardEntities, List<Card> openCards)
	{
		var allHoleCards = holeCardEntities
			.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
			.ToList();
		var baseballHand = new BaseballHand(allHoleCards, openCards, []);
		return HandDescriptionFormatter.GetHandDescription(baseballHand);
	}

	private static string? EvaluateSevenCardStudHandDescription(List<GameCard> holeCardEntities, List<Card> initialHoleCards, List<Card> openCards)
	{
		if (holeCardEntities.Count >= 3 && openCards.Count <= 4)
		{
			var downCard = new Card((Suit)holeCardEntities[2].Suit, (Symbol)holeCardEntities[2].Symbol);
			var studHand = new SevenCardStudHand(initialHoleCards, openCards, downCard);
			return HandDescriptionFormatter.GetHandDescription(studHand);
		}

		var allHoleCards = holeCardEntities
			.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
			.ToList();
		var partialStudHand = new StudHand(initialHoleCards, openCards, allHoleCards.Skip(2).ToList());
		return HandDescriptionFormatter.GetHandDescription(partialStudHand);
	}

	private static string EvaluateRazzHandDescription(List<GameCard> holeCardEntities, List<Card> openCards)
	{
		var allHoleCards = holeCardEntities
			.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
			.ToList();

		var initialHoleCards = allHoleCards.Take(2).ToList();
		var downCards = allHoleCards.Skip(2).ToList();
		var razzHand = new RazzHand(initialHoleCards, openCards, downCards);
		return RazzHand.GetLowHandDescription(razzHand.GetBestLowHand());
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

	/// <summary>
	/// Orders cards in the correct deal sequence, handling Seven Card Stud's multi-street dealing.
	/// For stud games, uses a composite key based on phase and location; for other games, falls back to DealOrder.
	/// </summary>
	/// <param name="cards">The collection of cards to order.</param>
	/// <param name="isSevenCardStud">Whether this is a Seven Card Stud game.</param>
	/// <returns>Cards ordered in the correct deal sequence.</returns>
	private static IEnumerable<GameCard> OrderCardsForDisplay(IEnumerable<GameCard> cards, bool isSevenCardStud)
	{
		if (isSevenCardStud)
		{
			return StudOrderHelper.OrderPlayerCards(
				cards,
				card => card.DealtAtPhase,
				card => card.Location == CardLocation.Hole,
				card => card.DealOrder);
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

	/// <summary>
	/// Finds the best 5-card hand from a set of cards (e.g., 7 cards in Hold'Em)
	/// by evaluating all C(n,5) combinations for the strongest hand.
	/// </summary>
	private static List<Card> FindBestFiveCardHand(List<Card> allCards)
	{
		if (allCards.Count <= 5)
		{
			return allCards;
		}

		var ranking = HandTypeStrengthRanking.Classic;
		List<Card>? bestCombo = null;
		long bestStrength = long.MinValue;

		foreach (var combo in allCards.SubsetsOfSize(5))
		{
			var comboList = combo.ToList();
			var handType = HandTypeDetermination.DetermineHandType(comboList);
			var strength = HandStrength.Calculate(comboList, handType, ranking);
			if (strength > bestStrength)
			{
				bestStrength = strength;
				bestCombo = comboList;
			}
		}

		return bestCombo ?? allCards.Take(5).ToList();
	}

	/// <summary>
	/// Finds the best 5-card Omaha hand using exactly 2 hole cards + 3 community cards.
	/// Evaluates all C(4,2) × C(5,3) = 60 valid combinations.
	/// </summary>
	private static List<Card> FindBestOmahaHand(List<Card> holeCards, List<Card> communityCards)
	{
		var ranking = HandTypeStrengthRanking.Classic;
		List<Card>? bestCombo = null;
		long bestStrength = long.MinValue;

		foreach (var holePair in holeCards.SubsetsOfSize(2))
		{
			foreach (var communityTriple in communityCards.SubsetsOfSize(3))
			{
				var combo = holePair.Concat(communityTriple).ToList();
				var handType = HandTypeDetermination.DetermineHandType(combo);
				var strength = HandStrength.Calculate(combo, handType, ranking);
				if (strength > bestStrength)
				{
					bestStrength = strength;
					bestCombo = combo;
				}
			}
		}

		return bestCombo ?? holeCards.Take(2).Concat(communityCards.Take(3)).ToList();
	}

	/// <summary>
	/// Finds the best 5-card Nebraska hand using exactly 3 hole cards + 2 community cards.
	/// Evaluates all C(5,3) × C(5,2) = 100 valid combinations.
	/// </summary>
	private static List<Card> FindBestNebraskaHand(List<Card> holeCards, List<Card> communityCards)
	{
		var ranking = HandTypeStrengthRanking.Classic;
		List<Card>? bestCombo = null;
		long bestStrength = long.MinValue;

		foreach (var holeTriple in holeCards.SubsetsOfSize(3))
		{
			foreach (var communityPair in communityCards.SubsetsOfSize(2))
			{
				var combo = holeTriple.Concat(communityPair).ToList();
				var handType = HandTypeDetermination.DetermineHandType(combo);
				var strength = HandStrength.Calculate(combo, handType, ranking);
				if (strength > bestStrength)
				{
					bestStrength = strength;
					bestCombo = combo;
				}
			}
		}

		return bestCombo ?? holeCards.Take(3).Concat(communityCards.Take(2)).ToList();
	}
}
