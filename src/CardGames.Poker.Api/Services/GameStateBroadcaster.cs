using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Broadcasts game state updates to connected SignalR clients.
/// </summary>
public sealed class GameStateBroadcaster : IGameStateBroadcaster
{
    private const int ClientDealAnimationInitialDelayMs = 100;
    private const int ClientDealAnimationStepDelayMs = 750;

    private readonly IHubContext<GameHub> _hubContext;
    private readonly ITableStateBuilder _tableStateBuilder;
    private readonly IActionTimerService _actionTimerService;
    private readonly IAutoActionService _autoActionService;
    private readonly ILogger<GameStateBroadcaster> _logger;

    /// <summary>
    /// Phases that require a player action timer.
    /// </summary>
    private static readonly HashSet<string> ActionPhases = new(StringComparer.OrdinalIgnoreCase)
    {
        "FirstBettingRound",
        "SecondBettingRound",
        "DrawPhase",
        "DropOrStay"
    };

    /// <summary>
    /// Group name prefix for game groups.
    /// </summary>
    private const string GameGroupPrefix = "game:";

    /// <summary>
    /// Initializes a new instance of the <see cref="GameStateBroadcaster"/> class.
    /// </summary>
    public GameStateBroadcaster(
        IHubContext<GameHub> hubContext,
        ITableStateBuilder tableStateBuilder,
        IActionTimerService actionTimerService,
        IAutoActionService autoActionService,
        ILogger<GameStateBroadcaster> logger)
    {
        _hubContext = hubContext;
        _tableStateBuilder = tableStateBuilder;
        _actionTimerService = actionTimerService;
        _autoActionService = autoActionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task BroadcastGameStateAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var groupName = GetGroupName(gameId);

        try
        {
            // Build and send public state to the entire group
            var publicState = await _tableStateBuilder.BuildPublicStateAsync(gameId, cancellationToken);
            if (publicState is not null)
            {
                _logger.LogInformation(
                    "Broadcasting public state for game {GameId}, phase: {Phase}, seats: {SeatCount}",
                    gameId, publicState.CurrentPhase, publicState.Seats.Count);

                await _hubContext.Clients.Group(groupName)
                    .SendAsync("TableStateUpdated", publicState, cancellationToken);

                _logger.LogDebug("Broadcast public state to group {GroupName}", groupName);

                // Manage action timer based on current phase and actor
                ManageActionTimer(gameId, publicState);
            }

            // Get all player user IDs and send private state to each
            var playerUserIds = await _tableStateBuilder.GetPlayerUserIdsAsync(gameId, cancellationToken);
            _logger.LogInformation(
                "Broadcasting private state to {PlayerCount} players: [{PlayerIds}]",
                playerUserIds.Count, string.Join(", ", playerUserIds));

            foreach (var userId in playerUserIds)
            {
                await SendPrivateStateToUserAsync(gameId, userId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting game state for game {GameId}", gameId);
            throw;
        }
    }

    /// <summary>
    /// Manages the action timer based on the current game state.
    /// Starts/restarts the timer when a new player needs to act, stops it otherwise.
    /// </summary>
    private void ManageActionTimer(Guid gameId, TableStatePublicDto state)
    {
        var existingTimer = _actionTimerService.GetTimerState(gameId);

        if (state.ChipCheckPause?.IsPaused == true)
        {
            if (existingTimer is not null && existingTimer.TimerType == ActionTimerType.ChipCheckPause)
            {
                return;
            }

            if (existingTimer is not null)
            {
                _logger.LogDebug("Stopping non-pause timer for game {GameId} because chip check pause is active", gameId);
                _actionTimerService.StopTimer(gameId);
            }

            return;
        }

        // Check if we're in a phase that requires player action
        var requiresAction = ActionPhases.Contains(state.CurrentPhase) ||
                             state.CurrentPhaseRequiresAction;

        var currentActorSeatIndex = state.CurrentActorSeatIndex;
        var isSimultaneousAction = string.Equals(state.CurrentPhase, "DropOrStay", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(state.GameTypeCode, PokerGameMetadataRegistry.BobBarkerCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(state.CurrentPhase, "DrawPhase", StringComparison.OrdinalIgnoreCase));

        if (!requiresAction || (currentActorSeatIndex < 0 && !isSimultaneousAction) || state.IsPaused)
        {
            // Stop timer if no action needed, no actor (and not simultaneous), or game is paused
            if (existingTimer is not null)
            {
                _logger.LogDebug("Stopping action timer for game {GameId} - no action required", gameId);
                _actionTimerService.StopTimer(gameId);
            }
            return;
        }

        // Check if the current actor has changed
        var effectiveActorSeatIndex = isSimultaneousAction ? -1 : currentActorSeatIndex;
        if (existingTimer is not null &&
            existingTimer.TimerType == ActionTimerType.PlayerAction &&
            existingTimer.PlayerSeatIndex == effectiveActorSeatIndex)
        {
            // Same player/state, timer already running - don't restart
            _logger.LogDebug(
                "Action timer already running for game {GameId}, player seat {SeatIndex}",
                gameId, effectiveActorSeatIndex);
            return;
        }

        // Start a new timer for the current actor
        _logger.LogInformation(
            "Starting action timer for game {GameId}, player seat {SeatIndex}, phase {Phase}",
            gameId, effectiveActorSeatIndex, state.CurrentPhase);

        var durationSeconds = IActionTimerService.DefaultTimerDurationSeconds;
        var startDelay = GetActionTimerStartDelay(state);

        // Use 30 seconds for short decision phases.
        if ((string.Equals(state.GameTypeCode, PokerGameMetadataRegistry.ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(state.CurrentPhase, "KeepOrTrade", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(state.GameTypeCode, PokerGameMetadataRegistry.TollboothCode, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(state.CurrentPhase, "TollboothOffer", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(state.GameTypeCode, PokerGameMetadataRegistry.InBetweenCode, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(state.CurrentPhase, "InBetweenTurn", StringComparison.OrdinalIgnoreCase)))
        {
            durationSeconds = 30;
        }

        _actionTimerService.StartTimer(
            gameId,
            effectiveActorSeatIndex,
            durationSeconds: durationSeconds,
            startDelay: startDelay,
            onExpired: async (gId, seatIndex) =>
            {
                _logger.LogInformation(
                    "Timer expired for game {GameId}, player seat {SeatIndex} - performing auto-action",
                    gId, seatIndex);
                await _autoActionService.PerformAutoActionAsync(gId, seatIndex);
            });
    }

    private static TimeSpan GetActionTimerStartDelay(TableStatePublicDto state)
    {
        if (string.Equals(state.GameTypeCode, PokerGameMetadataRegistry.KingsAndLowsCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(state.CurrentPhase, "DropOrStay", StringComparison.OrdinalIgnoreCase))
        {
            return CalculateKingsAndLowsDropOrStayStartDelay(state);
        }

        return TimeSpan.Zero;
    }

    private static TimeSpan CalculateKingsAndLowsDropOrStayStartDelay(TableStatePublicDto state)
    {
        var activeSeatCount = state.Seats.Count(static seat => seat.IsOccupied && !seat.IsSittingOut && !seat.IsFolded);
        if (activeSeatCount == 0)
        {
            return TimeSpan.Zero;
        }

        var cardsPerSeat = state.Seats
            .Where(static seat => seat.IsOccupied && !seat.IsSittingOut && !seat.IsFolded)
            .Select(static seat => seat.Cards.Count)
            .DefaultIfEmpty(0)
            .Max();

        if (cardsPerSeat == 0)
        {
            return TimeSpan.Zero;
        }

        var animationSteps = (activeSeatCount * cardsPerSeat) + 1;
        var totalDelayMs = ClientDealAnimationInitialDelayMs + (animationSteps * ClientDealAnimationStepDelayMs);
        return TimeSpan.FromMilliseconds(totalDelayMs);
    }

        /// <inheritdoc />
        public async Task BroadcastGameStateToUserAsync(Guid gameId, string userId, CancellationToken cancellationToken = default)
        {
            var groupName = GetGroupName(gameId);

            try
            {
            // Send public state to the user
            var publicState = await _tableStateBuilder.BuildPublicStateAsync(gameId, cancellationToken);
            if (publicState is not null)
            {
                await _hubContext.Clients.User(userId)
                    .SendAsync("TableStateUpdated", publicState, cancellationToken);
            }

            // Send private state to the user
            await SendPrivateStateToUserAsync(gameId, userId, cancellationToken);

            _logger.LogDebug("Broadcast state to user {UserId} for game {GameId}", userId, gameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting game state to user {UserId} for game {GameId}", userId, gameId);
            throw;
        }
    }

        private async Task SendPrivateStateToUserAsync(Guid gameId, string userId, CancellationToken cancellationToken)
        {
            var privateState = await _tableStateBuilder.BuildPrivateStateAsync(gameId, userId, cancellationToken);
            if (privateState is not null)
            {
                _logger.LogInformation(
                    "Sending private state to user {UserId} for game {GameId}: {CardCount} cards, seat {SeatPosition}",
                    userId, gameId, privateState.Hand.Count, privateState.SeatPosition);

                await _hubContext.Clients.User(userId)
                    .SendAsync("PrivateStateUpdated", privateState, cancellationToken);

                _logger.LogDebug("Sent private state to user {UserId} for game {GameId}", userId, gameId);
            }
            else
            {
                _logger.LogWarning(
                    "BuildPrivateStateAsync returned null for user {UserId} in game {GameId}",
                    userId, gameId);
            }
        }

        /// <inheritdoc />
        public async Task BroadcastPlayerJoinedAsync(
            Guid gameId,
            string playerName,
            int seatIndex,
            bool canPlayCurrentHand,
            CancellationToken cancellationToken = default)
        {
            var groupName = GetGroupName(gameId);

            try
            {
                var notification = new PlayerJoinedDto
                {
                    GameId = gameId,
                    PlayerName = playerName,
                    SeatIndex = seatIndex,
                    CanPlayCurrentHand = canPlayCurrentHand,
                    Message = canPlayCurrentHand
                        ? $"{playerName} has joined the table!"
                        : $"{playerName} has joined and will play next hand."
                };

                // Send to all in the group except the player who just joined
                await _hubContext.Clients.GroupExcept(groupName, [playerName])
                    .SendAsync("PlayerJoined", notification, cancellationToken);

                _logger.LogInformation(
                    "Broadcast PlayerJoined notification for {PlayerName} at seat {SeatIndex} in game {GameId}",
                    playerName, seatIndex, gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to broadcast PlayerJoined notification for game {GameId}", gameId);
                // Don't throw - the join was successful, just the notification failed
            }
        }

                /// <inheritdoc />
                public async Task BroadcastTableToastAsync(
                    TableToastNotificationDto notification,
                    CancellationToken cancellationToken = default)
                {
                    var groupName = GetGroupName(notification.GameId);

                    try
                    {
                        await _hubContext.Clients.Group(groupName)
                            .SendAsync("TableToastNotification", notification, cancellationToken);

                        _logger.LogInformation(
                            "Broadcast TableToastNotification for game {GameId}: {Message}",
                            notification.GameId,
                            notification.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to broadcast TableToastNotification for game {GameId}",
                            notification.GameId);
                    }
                }

                /// <inheritdoc />
                public async Task BroadcastTableSettingsUpdatedAsync(
                    TableSettingsUpdatedDto notification,
                    CancellationToken cancellationToken = default)
                {
                    var groupName = GetGroupName(notification.GameId);

                            try
                            {
                                await _hubContext.Clients.Group(groupName)
                                    .SendAsync("TableSettingsUpdated", notification, cancellationToken);

                                _logger.LogInformation(
                                    "Broadcast TableSettingsUpdated notification for game {GameId} by user {UserId}",
                                    notification.GameId,
                                    notification.UpdatedById);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex,
                                    "Failed to broadcast TableSettingsUpdated notification for game {GameId}",
                                    notification.GameId);
                                throw;
                            }
                        }

                        /// <inheritdoc />
                        public async Task BroadcastOddsVisibilityUpdatedAsync(
                            OddsVisibilityUpdatedDto notification,
                            CancellationToken cancellationToken = default)
                        {
                            var groupName = GetGroupName(notification.GameId);

                            try
                            {
                                await _hubContext.Clients.Group(groupName)
                                    .SendAsync("OddsVisibilityUpdated", notification, cancellationToken);

                                _logger.LogInformation(
                                    "Broadcast OddsVisibilityUpdated notification for game {GameId} by user {UserId}",
                                    notification.GameId,
                                    notification.UpdatedById);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex,
                                    "Failed to broadcast OddsVisibilityUpdated notification for game {GameId}",
                                    notification.GameId);
                                throw;
                            }
                        }

                        /// <inheritdoc />
                        public async Task BroadcastPlayerActionAsync(
                            Guid gameId,
                            int seatIndex,
                            string? playerName,
                            string actionDescription,
                            CancellationToken cancellationToken = default)
                        {
                            var groupName = GetGroupName(gameId);

                            try
                            {
                                var notification = new PlayerActionPerformedDto
                                {
                                    GameId = gameId,
                                    SeatIndex = seatIndex,
                                    PlayerName = playerName,
                                    ActionDescription = actionDescription,
                                    PerformedAtUtc = DateTimeOffset.UtcNow,
                                    DisplayDurationSeconds = 5
                                };

                                await _hubContext.Clients.Group(groupName)
                                    .SendAsync("PlayerActionPerformed", notification, cancellationToken);

                                _logger.LogDebug(
                                    "Broadcast PlayerActionPerformed for game {GameId}, seat {SeatIndex}: {Action}",
                                    gameId, seatIndex, actionDescription);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex,
                                    "Failed to broadcast PlayerActionPerformed for game {GameId}, seat {SeatIndex}",
                                    gameId, seatIndex);
                                // Don't throw - the action was successful, just the notification failed
                            }
                        }

                        private static string GetGroupName(Guid gameId) => $"{GameGroupPrefix}{gameId}";
                    }
