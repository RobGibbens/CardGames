using CardGames.Contracts.SignalR;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands;
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
    private readonly ILogger<TableStateBuilder> _logger;

	private sealed record UserProfile(string? FirstName, string? AvatarUrl);

    /// <summary>
    /// Initializes a new instance of the <see cref="TableStateBuilder"/> class.
    /// </summary>
    public TableStateBuilder(CardsDbContext context, ILogger<TableStateBuilder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TableStatePublicDto?> BuildPublicStateAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var game = await _context.Games
            .Include(g => g.GameType)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game is null)
        {
            _logger.LogWarning("Game {GameId} not found when building public state", gameId);
            return null;
        }

        var gamePlayers = await _context.GamePlayers
            .Where(gp => gp.GameId == gameId)
            .Include(gp => gp.Player)
            .Include(gp => gp.Cards)
            .OrderBy(gp => gp.SeatPosition)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

            // Calculate total pot from current betting round contributions
            var totalPot = await CalculateTotalPotAsync(gameId, game.CurrentHandNumber, cancellationToken);

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
				.Select(gp => BuildSeatPublicDto(gp, game.CurrentHandNumber, game.Ante ?? 0, userProfilesByEmail))
						.ToList();

					// Calculate results phase state
					var isResultsPhase = game.CurrentPhase == "Complete" && game.HandCompletedAt.HasValue;
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
						SpecialRules = BuildSpecialRulesDto(rules)
					};
				}

    /// <inheritdoc />
    public async Task<PrivateStateDto?> BuildPrivateStateAsync(Guid gameId, string userId, CancellationToken cancellationToken = default)
    {
        var game = await _context.Games
            .Include(g => g.GameType)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game is null)
        {
            return null;
        }

        // Find the player by matching the authenticated user id.
        // SignalR `Clients.User(userId)` now routes by email claim, so prefer email/name matching.
        var gamePlayer = await _context.GamePlayers
            .Where(gp => gp.GameId == gameId)
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
            return null;
        }

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

            // Draw games (no community cards). Provide a description once 5 cards are available.
            // (During seating / pre-deal phases there may be 0-4 cards and we keep the description null.)
            if (communityCards.Count == 0 && playerCards.Count >= 5)
            {
                // Twos, Jacks, Man with the Axe uses wild cards.
                // The base `DrawHand` evaluator ignores wild substitutions,
                // so we must use the variant-specific hand type here.
                HandBase drawHand;
                if (string.Equals(game.GameType?.Code, "TWOSJACKSMANWITHTHEAXE", StringComparison.OrdinalIgnoreCase))
                {
                    drawHand = new CardGames.Poker.Hands.DrawHands.TwosJacksManWithTheAxeDrawHand(playerCards);
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
			var draw = BuildDrawPrivateDto(game, gamePlayer);

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
				IsMyTurn = isMyTurn,
				HandHistory = handHistory
			};
		}

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetPlayerUserIdsAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        return await _context.GamePlayers
            .Where(gp => gp.GameId == gameId)
            .Include(gp => gp.Player)
            .AsNoTracking()
            // Must align with the key used by SignalR `Clients.User(userId)` - now email-based.
            .Select(gp => gp.Player.Email ?? gp.Player.Name ?? gp.Player.ExternalId)
            .ToListAsync(cancellationToken);
    }

    private static SeatPublicDto BuildSeatPublicDto(
		GamePlayer gamePlayer,
		int currentHandNumber,
		int ante,
		IReadOnlyDictionary<string, UserProfile> userProfilesByEmail)
    {
        // Get current hand cards (not discarded)
        var playerCards = gamePlayer.Cards
            .Where(c => !c.IsDiscarded && c.HandNumber == currentHandNumber)
            .OrderBy(c => c.DealOrder)
            .ToList();

        // Cards are shown face-down (as placeholders) in public view
        var publicCards = playerCards.Select(_ => new CardPublicDto
        {
            IsFaceUp = false,
            Rank = null,
            Suit = null
        }).ToList();

        // Determine sitting out reason
        string? sittingOutReason = null;
        if (gamePlayer.IsSittingOut)
        {
            if (gamePlayer.ChipStack < ante && ante > 0)
            {
                sittingOutReason = "Insufficient chips";
            }
            else
            {
                sittingOutReason = "Sitting out";
            }
        }

        string? playerFirstName = null;
        string? playerAvatarUrl = null;

        if (!string.IsNullOrWhiteSpace(gamePlayer.Player.Email)
			&& userProfilesByEmail.TryGetValue(gamePlayer.Player.Email, out var profile))
        {
			playerFirstName = profile.FirstName;
			playerAvatarUrl = profile.AvatarUrl;
        }

		// Fallback for existing/legacy data in the poker player table.
		playerAvatarUrl ??= gamePlayer.Player.AvatarUrl;

        return new SeatPublicDto
        {
            SeatIndex = gamePlayer.SeatPosition,
            IsOccupied = true,
            PlayerName = gamePlayer.Player.Name,
            PlayerFirstName = string.IsNullOrWhiteSpace(playerFirstName) ? null : playerFirstName.Trim(),
            PlayerAvatarUrl = string.IsNullOrWhiteSpace(playerAvatarUrl) ? null : playerAvatarUrl.Trim(),
            Chips = gamePlayer.ChipStack,
            IsReady = gamePlayer.Status == Entities.GamePlayerStatus.Active && !gamePlayer.IsSittingOut,
            IsFolded = gamePlayer.HasFolded,
            IsAllIn = gamePlayer.IsAllIn,
            IsDisconnected = !gamePlayer.IsConnected,
            IsSittingOut = gamePlayer.IsSittingOut,
            SittingOutReason = sittingOutReason,
            CurrentBet = gamePlayer.CurrentBet,
            Cards = publicCards
        };
    }

    private List<CardPrivateDto> BuildPrivateHand(GamePlayer gamePlayer, int currentHandNumber)
    {
        return gamePlayer.Cards
            .Where(c => !c.IsDiscarded && c.HandNumber == currentHandNumber)
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
        var bettingPhases = new[] { "FirstBettingRound", "SecondBettingRound" };
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
        GamePlayer gamePlayer)
    {
        if (game.CurrentPhase != "DrawPhase")
        {
            return null;
        }

        // Check if player has an Ace in their current hand to allow 4 discards
        var playerCards = gamePlayer.Cards
            .Where(c => !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber)
            .ToList();

        var hasAce = playerCards.Any(c => c.Symbol == Data.Entities.CardSymbol.Ace);
        var maxDiscards = hasAce ? 4 : 3;

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
        if (game.CurrentPhase != "Showdown" && game.CurrentPhase != "Complete")
        {
            return null;
        }

		var isTwosJacksAxe = string.Equals(
			game.GameType?.Code,
			PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode,
			StringComparison.OrdinalIgnoreCase);

        // Evaluate all hands for players who haven't folded
        // Use FiveCardHand as the base type since both DrawHand and TwosJacksManWithTheAxeDrawHand inherit from it
        var playerHandEvaluations = new Dictionary<string, (FiveCardHand hand, TwosJacksManWithTheAxeDrawHand? wildHand, GamePlayer gamePlayer, List<GameCard> cards, List<int> wildIndexes)>();

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
                    playerHandEvaluations[gp.Player.Name] = (wildHand, wildHand, gp, cards, wildIndexes);
                }
                else
                {
                    var drawHand = new DrawHand(coreCards);
                    playerHandEvaluations[gp.Player.Name] = (drawHand, null, gp, cards, []);
                }
            }
        }

        // Determine winners
        var highHandWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sevensWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sevensPoolRolledOver = false;

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
                foreach (var kvp in playerHandEvaluations.Where(k => k.Value.wildHand?.HasNaturalPairOfSevens() == true))
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

                return new ShowdownPlayerResultDto
                {
                    PlayerName = gp.Player.Name,
                    PlayerFirstName = userProfile?.FirstName,
                    SeatPosition = gp.SeatPosition,
                    HandRanking = handRanking,
                    HandDescription = playerHandEvaluations.TryGetValue(gp.Player.Name, out var e)
                        ? HandDescriptionFormatter.GetHandDescription(e.hand)
                        : null,
					AmountWon = 0, // Actual payout calculated separately
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
            SevensPoolRolledOver = sevensPoolRolledOver
        };
    }

    private async Task<int> CalculateTotalPotAsync(Guid gameId, int handNumber, CancellationToken cancellationToken)
    {
        var totalContributions = await _context.GamePlayers
            .Where(gp => gp.GameId == gameId)
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

        /// <summary>
        /// Retrieves hand history entries for the dashboard.
        /// </summary>
        private async Task<List<HandHistoryEntryDto>> GetHandHistoryEntriesAsync(
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

                // Get current player's result if specified
                string? currentPlayerResultLabel = null;
                var currentPlayerNetDelta = 0;
                var currentPlayerWon = false;

                if (currentUserPlayerId.HasValue)
                {
                    var currentPlayerResult = h.PlayerResults
                        .FirstOrDefault(pr => pr.PlayerId == currentUserPlayerId.Value);

                    if (currentPlayerResult != null)
                    {
                        currentPlayerResultLabel = currentPlayerResult.GetResultLabel();
                        currentPlayerNetDelta = currentPlayerResult.NetChipDelta;
                        currentPlayerWon = currentPlayerResult.ResultType == Entities.PlayerResultType.Won ||
                                           currentPlayerResult.ResultType == Entities.PlayerResultType.SplitPotWon;
                    }
                }


                return new HandHistoryEntryDto(
	                amountWon: totalWinnings,
	                completedAtUtc: h.CompletedAtUtc,
	                currentPlayerNetDelta: currentPlayerNetDelta,
	                currentPlayerResultLabel: currentPlayerResultLabel,
	                currentPlayerWon: currentPlayerWon,
	                handNumber: h.HandNumber,
	                winnerCount: h.Winners.Count,
	                winnerName: winnerDisplay,
	                winningHandDescription: h.WinningHandDescription,
	                wonByFold: h.EndReason == Data.Entities.HandEndReason.FoldedToWinner
                );
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
    }
