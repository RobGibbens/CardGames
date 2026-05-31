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

/// <summary>
/// Builds table state snapshots for SignalR broadcasts.
/// </summary>
public sealed partial class TableStateBuilder : ITableStateBuilder
{

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

		var serverUtcNow = DateTimeOffset.UtcNow;

		// Calculate results phase state
		var isResultsPhase = (game.CurrentPhase == "Complete" || game.CurrentPhase == "PotMatching") && game.HandCompletedAt.HasValue;
		int? secondsUntilNextHand = null;
		if (isResultsPhase && game.NextHandStartsAt.HasValue)
		{
			var remaining = game.NextHandStartsAt.Value - serverUtcNow;
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
		var isBobBarkerShowdownReveal = IsBobBarkerGame(game.GameType?.Code)
			&& (string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(game.CurrentPhase, "Complete", StringComparison.OrdinalIgnoreCase));

		// Build community cards for variants that use table cards (e.g., Good Bad Ugly)
		var communityCards = await _context.GameCards
			.Where(c => c.GameId == gameId
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

		var showdown = await BuildShowdownPublicDtoAsync(game, gamePlayers, userProfilesByEmail, cancellationToken);
		var soundEffects = TableSoundEffectBuilder.Build(game, showdown);

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
			ServerUtcNow = serverUtcNow,
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
		if (gamePlayer.Status == GamePlayerStatus.Eliminated)
		{
			return "Observing";
		}

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

}
