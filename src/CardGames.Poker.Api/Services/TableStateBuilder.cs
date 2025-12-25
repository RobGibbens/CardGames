using CardGames.Contracts.SignalR;
using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;
using CardGames.Poker.Hands.DrawHands;
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

					return new TableStatePublicDto
					{
						GameId = game.Id,
						Name = game.Name,
						CurrentPhase = game.CurrentPhase,
						CurrentPhaseDescription = PhaseDescriptionResolver.TryResolve(game.GameType?.Code, game.CurrentPhase),
						Ante = game.Ante ?? 0,
						MinBet = game.MinBet ?? 0,
						TotalPot = totalPot,
						DealerSeatIndex = game.DealerPosition,
						CurrentActorSeatIndex = game.CurrentPlayerIndex,
						IsPaused = game.Status == GameStatus.BetweenHands,
						CurrentHandNumber = game.CurrentHandNumber,
						CreatedByName = game.CreatedByName,
						Seats = seats,
						Showdown = BuildShowdownPublicDto(game, gamePlayers),
						HandCompletedAtUtc = game.HandCompletedAt,
						NextHandStartsAtUtc = game.NextHandStartsAt,
						IsResultsPhase = isResultsPhase,
						SecondsUntilNextHand = secondsUntilNextHand,
						HandHistory = handHistory
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

            var allEvaluationCards = playerCards.Concat(communityCards).ToList();

            // Five Card Draw (exactly 5 known cards).
            if (playerCards.Count == 5 && communityCards.Count == 0)
            {
                var drawHand = new DrawHand(playerCards);
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

        return new PrivateStateDto
        {
            GameId = gameId,
            PlayerName = gamePlayer.Player.Name,
            SeatPosition = gamePlayer.SeatPosition,
            Hand = hand,
			HandEvaluationDescription = handEvaluationDescription,
            AvailableActions = availableActions,
            Draw = draw,
            IsMyTurn = isMyTurn
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
            IsReady = gamePlayer.Status == GamePlayerStatus.Active && !gamePlayer.IsSittingOut,
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

        return new DrawPrivateDto
        {
            IsMyTurnToDraw = game.CurrentDrawPlayerIndex == gamePlayer.SeatPosition,
            MaxDiscards = 3, // Standard five card draw rule
            HasDrawnThisRound = gamePlayer.HasDrawnThisRound
        };
    }

    private static ShowdownPublicDto? BuildShowdownPublicDto(
        Game game,
        List<GamePlayer> gamePlayers)
    {
        if (game.CurrentPhase != "Showdown" && game.CurrentPhase != "Complete")
        {
            return null;
        }

        // For showdown, cards should be visible
        var playerResults = gamePlayers
            .Where(gp => !gp.HasFolded)
            .Select(gp => new ShowdownPlayerResultDto
            {
                PlayerName = gp.Player.Name,
                SeatPosition = gp.SeatPosition,
                HandRanking = null, // TODO: Calculate hand ranking from domain logic
                AmountWon = 0, // TODO: Get from showdown results
                IsWinner = false, // TODO: Get from showdown results
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
            })
            .ToList();

        return new ShowdownPublicDto
        {
            PlayerResults = playerResults,
            IsComplete = game.CurrentPhase == "Complete"
        };
    }

    private async Task<int> CalculateTotalPotAsync(Guid gameId, int handNumber, CancellationToken cancellationToken)
    {
        var totalContributions = await _context.GamePlayers
            .Where(gp => gp.GameId == gameId)
            .SumAsync(gp => gp.TotalContributedThisHand, cancellationToken);

        return totalContributions;
    }

        private static string MapSymbolToRank(CardSymbol symbol)
        {
            return symbol switch
            {
                CardSymbol.Ace => "A",
                CardSymbol.King => "K",
                CardSymbol.Queen => "Q",
                CardSymbol.Jack => "J",
                CardSymbol.Ten => "10",
                CardSymbol.Nine => "9",
                CardSymbol.Eight => "8",
                CardSymbol.Seven => "7",
                CardSymbol.Six => "6",
                CardSymbol.Five => "5",
                CardSymbol.Four => "4",
                CardSymbol.Three => "3",
                CardSymbol.Deuce => "2",
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
                .Where(h => h.GameId == gameId)
                .OrderByDescending(h => h.CompletedAtUtc)
                .Take(take)
                .Include(h => h.Winners)
                .Include(h => h.PlayerResults)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return histories.Select(h =>
            {
                // Get winner display
                var winnerDisplay = h.Winners.Count switch
                {
                    0 => "Unknown",
                    1 => h.Winners.First().PlayerName,
                    _ => $"{h.Winners.First().PlayerName} +{h.Winners.Count - 1}"
                };

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

                return new HandHistoryEntryDto
                {
                    HandNumber = h.HandNumber,
                    CompletedAtUtc = h.CompletedAtUtc,
                    WinnerName = winnerDisplay,
                    AmountWon = totalWinnings,
                    WinningHandDescription = h.WinningHandDescription,
                    WonByFold = h.EndReason == Entities.HandEndReason.FoldedToWinner,
                    WinnerCount = h.Winners.Count,
                    CurrentPlayerResultLabel = currentPlayerResultLabel,
                    CurrentPlayerNetDelta = currentPlayerNetDelta,
                    CurrentPlayerWon = currentPlayerWon
                };
            }).ToList();
        }
    }
