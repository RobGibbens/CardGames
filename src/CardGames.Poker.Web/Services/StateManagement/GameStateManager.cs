using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.Events;

namespace CardGames.Poker.Web.Services.StateManagement;

/// <summary>
/// Implementation of the game state manager for Blazor clients.
/// Manages local state with support for optimistic updates, state reconciliation, and event replay.
/// </summary>
public class GameStateManager : IGameStateManager
{
    private readonly ILogger<GameStateManager> _logger;
    private readonly object _stateLock = new();
    private readonly Dictionary<Guid, PendingOptimisticUpdate> _pendingUpdates = new();
    private readonly List<StateSnapshot> _snapshotHistory = new();
    private readonly int _maxSnapshotHistory = 10;

    private GameStateSnapshot? _currentState;
    private long _stateVersion;

    public GameStateSnapshot? CurrentState => _currentState;
    public bool HasPendingUpdates => _pendingUpdates.Count > 0;
    public long StateVersion => _stateVersion;

    public event Action<GameStateSnapshot>? OnStateChanged;
    public event Action<StateConflict>? OnStateConflict;
    public event Action<OptimisticUpdateResult>? OnOptimisticUpdateResult;

    public GameStateManager(ILogger<GameStateManager> logger)
    {
        _logger = logger;
    }

    public void Initialize(Guid tableId, TableStateSnapshot snapshot)
    {
        lock (_stateLock)
        {
            _currentState = ConvertToGameStateSnapshot(tableId, snapshot, 1);
            _stateVersion = 1;
            _pendingUpdates.Clear();
            _snapshotHistory.Clear();

            _logger.LogInformation(
                "State manager initialized for table {TableId}, version {Version}",
                tableId, _stateVersion);
        }

        NotifyStateChanged();
    }

    public void ApplyServerEvent(GameEvent gameEvent)
    {
        lock (_stateLock)
        {
            if (_currentState is null)
            {
                _logger.LogWarning("Cannot apply event - state not initialized");
                return;
            }

            var newState = ApplyEventToState(_currentState, gameEvent);
            if (newState != _currentState)
            {
                _stateVersion++;
                _currentState = newState with { Version = _stateVersion, LastUpdated = DateTime.UtcNow };

                _logger.LogDebug(
                    "Applied server event {EventType} to state, version now {Version}",
                    gameEvent.GetType().Name, _stateVersion);
            }
        }

        NotifyStateChanged();
    }

    public Guid ApplyOptimisticUpdate(OptimisticUpdate update)
    {
        var updateId = Guid.NewGuid();

        lock (_stateLock)
        {
            if (_currentState is null)
            {
                _logger.LogWarning("Cannot apply optimistic update - state not initialized");
                return updateId;
            }

            var preUpdateState = _currentState;
            var newState = ApplyOptimisticUpdateToState(_currentState, update);

            if (newState != _currentState)
            {
                _stateVersion++;
                _currentState = newState with { Version = _stateVersion, LastUpdated = DateTime.UtcNow };

                _pendingUpdates[updateId] = new PendingOptimisticUpdate(
                    updateId,
                    update,
                    DateTime.UtcNow,
                    preUpdateState);

                _logger.LogDebug(
                    "Applied optimistic update {UpdateId} ({UpdateType}), version now {Version}",
                    updateId, update.Type, _stateVersion);
            }
        }

        NotifyStateChanged();
        return updateId;
    }

    public void ConfirmOptimisticUpdate(Guid updateId)
    {
        lock (_stateLock)
        {
            if (_pendingUpdates.Remove(updateId, out var pending))
            {
                _logger.LogDebug(
                    "Confirmed optimistic update {UpdateId} ({UpdateType})",
                    updateId, pending.Update.Type);

                OnOptimisticUpdateResult?.Invoke(new OptimisticUpdateResult(updateId, true));
            }
            else
            {
                _logger.LogDebug("Update {UpdateId} not found in pending updates", updateId);
            }
        }
    }

    public void RejectOptimisticUpdate(Guid updateId, string reason)
    {
        lock (_stateLock)
        {
            if (_pendingUpdates.Remove(updateId, out var pending))
            {
                _logger.LogWarning(
                    "Rejected optimistic update {UpdateId} ({UpdateType}): {Reason}",
                    updateId, pending.Update.Type, reason);

                // Rollback to pre-update state if available
                if (pending.PreUpdateState is not null)
                {
                    _currentState = pending.PreUpdateState;
                    _stateVersion = pending.PreUpdateState.Version;
                }

                OnOptimisticUpdateResult?.Invoke(new OptimisticUpdateResult(updateId, false, reason));
            }
        }

        NotifyStateChanged();
    }

    public StateSnapshot CreateSnapshot()
    {
        lock (_stateLock)
        {
            if (_currentState is null)
            {
                throw new InvalidOperationException("Cannot create snapshot - state not initialized");
            }

            var snapshot = new StateSnapshot(
                Guid.NewGuid(),
                DateTime.UtcNow,
                _stateVersion,
                _currentState,
                _pendingUpdates.Values.ToList());

            _snapshotHistory.Add(snapshot);

            // Trim history if needed
            while (_snapshotHistory.Count > _maxSnapshotHistory)
            {
                _snapshotHistory.RemoveAt(0);
            }

            _logger.LogDebug(
                "Created snapshot {SnapshotId} at version {Version}",
                snapshot.Id, snapshot.Version);

            return snapshot;
        }
    }

    public void RestoreSnapshot(StateSnapshot snapshot)
    {
        lock (_stateLock)
        {
            _currentState = snapshot.State;
            _stateVersion = snapshot.Version;
            _pendingUpdates.Clear();

            foreach (var pending in snapshot.PendingUpdates)
            {
                _pendingUpdates[pending.Id] = pending;
            }

            _logger.LogInformation(
                "Restored snapshot {SnapshotId} at version {Version}",
                snapshot.Id, snapshot.Version);
        }

        NotifyStateChanged();
    }

    public StateValidationResult ValidateAgainstServer(TableStateSnapshot serverState)
    {
        lock (_stateLock)
        {
            if (_currentState is null)
            {
                return new StateValidationResult(false, [], 0, 0);
            }

            var conflicts = new List<StateConflict>();

            // Validate pot
            if (_currentState.Pot != serverState.Pot)
            {
                conflicts.Add(new StateConflict(
                    "Pot",
                    _currentState.Pot,
                    serverState.Pot,
                    $"Pot mismatch: client={_currentState.Pot}, server={serverState.Pot}"));
            }

            // Validate current bet
            if (_currentState.CurrentBet != serverState.CurrentBet)
            {
                conflicts.Add(new StateConflict(
                    "CurrentBet",
                    _currentState.CurrentBet,
                    serverState.CurrentBet,
                    $"Current bet mismatch: client={_currentState.CurrentBet}, server={serverState.CurrentBet}"));
            }

            // Validate game state
            if (_currentState.State != serverState.State)
            {
                conflicts.Add(new StateConflict(
                    "State",
                    _currentState.State,
                    serverState.State,
                    $"Game state mismatch: client={_currentState.State}, server={serverState.State}"));
            }

            // Validate current player
            if (_currentState.CurrentPlayerName != serverState.CurrentPlayerName)
            {
                conflicts.Add(new StateConflict(
                    "CurrentPlayerName",
                    _currentState.CurrentPlayerName,
                    serverState.CurrentPlayerName,
                    $"Current player mismatch: client={_currentState.CurrentPlayerName}, server={serverState.CurrentPlayerName}"));
            }

            // Validate player chip stacks
            foreach (var serverPlayer in serverState.Players)
            {
                var clientPlayer = _currentState.Players.FirstOrDefault(p => p.Name == serverPlayer.Name);
                if (clientPlayer is not null)
                {
                    if (clientPlayer.ChipStack != serverPlayer.ChipStack)
                    {
                        conflicts.Add(new StateConflict(
                            $"Players[{serverPlayer.Name}].ChipStack",
                            clientPlayer.ChipStack,
                            serverPlayer.ChipStack,
                            $"Chip stack mismatch for {serverPlayer.Name}: client={clientPlayer.ChipStack}, server={serverPlayer.ChipStack}"));
                    }

                    if (clientPlayer.HasFolded != serverPlayer.HasFolded)
                    {
                        conflicts.Add(new StateConflict(
                            $"Players[{serverPlayer.Name}].HasFolded",
                            clientPlayer.HasFolded,
                            serverPlayer.HasFolded,
                            $"Folded status mismatch for {serverPlayer.Name}: client={clientPlayer.HasFolded}, server={serverPlayer.HasFolded}"));
                    }
                }
            }

            // Notify any conflicts
            foreach (var conflict in conflicts)
            {
                OnStateConflict?.Invoke(conflict);
            }

            var isValid = conflicts.Count == 0;
            if (!isValid)
            {
                _logger.LogWarning(
                    "State validation failed with {ConflictCount} conflicts",
                    conflicts.Count);
            }

            return new StateValidationResult(
                isValid,
                conflicts,
                _stateVersion,
                serverState.HandNumber);
        }
    }

    public void ReconcileWithServer(TableStateSnapshot serverState)
    {
        lock (_stateLock)
        {
            if (_currentState is null)
            {
                _logger.LogWarning("Cannot reconcile - state not initialized");
                return;
            }

            // Server state is authoritative - replace local state
            _stateVersion++;
            _currentState = ConvertToGameStateSnapshot(
                _currentState.TableId,
                serverState,
                _stateVersion);

            // Clear pending updates as server state supersedes them
            _pendingUpdates.Clear();

            _logger.LogInformation(
                "Reconciled with server state, version now {Version}",
                _stateVersion);
        }

        NotifyStateChanged();
    }

    public void ReplayEvents(IEnumerable<GameEvent> events)
    {
        lock (_stateLock)
        {
            if (_currentState is null)
            {
                _logger.LogWarning("Cannot replay events - state not initialized");
                return;
            }

            var eventCount = 0;
            foreach (var gameEvent in events)
            {
                var newState = ApplyEventToState(_currentState, gameEvent);
                if (newState != _currentState)
                {
                    _stateVersion++;
                    _currentState = newState with { Version = _stateVersion, LastUpdated = DateTime.UtcNow };
                    eventCount++;
                }
            }

            _logger.LogInformation(
                "Replayed {EventCount} events, version now {Version}",
                eventCount, _stateVersion);
        }

        NotifyStateChanged();
    }

    public void Reset()
    {
        lock (_stateLock)
        {
            _currentState = null;
            _stateVersion = 0;
            _pendingUpdates.Clear();
            _snapshotHistory.Clear();

            _logger.LogInformation("State manager reset");
        }
    }

    private GameStateSnapshot ApplyEventToState(GameStateSnapshot state, GameEvent gameEvent)
    {
        return gameEvent switch
        {
            BettingActionEvent e => ApplyBettingAction(state, e),
            PlayerTurnEvent e => ApplyPlayerTurn(state, e),
            CommunityCardsDealtEvent e => ApplyCommunityCards(state, e),
            HandStartedEvent e => ApplyHandStarted(state, e),
            GameStateChangedEvent e => ApplyGameStateChanged(state, e),
            PlayerJoinedEvent e => ApplyPlayerJoined(state, e),
            PlayerLeftEvent e => ApplyPlayerLeft(state, e),
            PlayerConnectedEvent e => ApplyPlayerConnected(state, e),
            PlayerDisconnectedEvent e => ApplyPlayerDisconnected(state, e),
            PlayerReconnectedEvent e => ApplyPlayerReconnected(state, e),
            _ => state // Unhandled event types don't modify state
        };
    }

    private GameStateSnapshot ApplyBettingAction(GameStateSnapshot state, BettingActionEvent e)
    {
        var players = state.Players.ToList();
        var playerIndex = players.FindIndex(p => p.Name == e.Action.PlayerName);

        if (playerIndex < 0) return state;

        var player = players[playerIndex];
        var newPlayer = player with
        {
            ChipStack = e.Action.ActionType switch
            {
                BettingActionType.Bet => player.ChipStack - e.Action.Amount,
                BettingActionType.Call => player.ChipStack - e.Action.Amount,
                BettingActionType.Raise => player.ChipStack - e.Action.Amount,
                BettingActionType.AllIn => 0,
                _ => player.ChipStack
            },
            CurrentBet = e.Action.ActionType switch
            {
                BettingActionType.Bet => player.CurrentBet + e.Action.Amount,
                BettingActionType.Call => player.CurrentBet + e.Action.Amount,
                BettingActionType.Raise => player.CurrentBet + e.Action.Amount,
                BettingActionType.AllIn => player.ChipStack + player.CurrentBet,
                _ => player.CurrentBet
            },
            HasFolded = e.Action.ActionType == BettingActionType.Fold || player.HasFolded,
            IsAllIn = e.Action.ActionType == BettingActionType.AllIn || player.IsAllIn
        };

        players[playerIndex] = newPlayer;

        return state with
        {
            Players = players,
            Pot = e.PotAfterAction,
            CurrentBet = e.Action.ActionType switch
            {
                BettingActionType.Bet => e.Action.Amount,
                BettingActionType.Raise => e.Action.Amount,
                _ => state.CurrentBet
            }
        };
    }

    private GameStateSnapshot ApplyPlayerTurn(GameStateSnapshot state, PlayerTurnEvent e)
    {
        var players = state.Players.Select(p => p with
        {
            IsCurrentPlayer = p.Name == e.PlayerName
        }).ToList();

        return state with
        {
            Players = players,
            CurrentPlayerName = e.PlayerName
        };
    }

    private GameStateSnapshot ApplyCommunityCards(GameStateSnapshot state, CommunityCardsDealtEvent e)
    {
        return state with
        {
            CommunityCards = e.AllCommunityCards,
            CurrentStreet = e.StreetName
        };
    }

    private GameStateSnapshot ApplyHandStarted(GameStateSnapshot state, HandStartedEvent e)
    {
        var players = state.Players.Select(p => p with
        {
            CurrentBet = 0,
            HasFolded = false,
            IsAllIn = false,
            HoleCards = null,
            IsDealer = p.Position == e.DealerPosition
        }).ToList();

        return state with
        {
            Players = players,
            HandNumber = e.HandNumber,
            DealerPosition = e.DealerPosition,
            SmallBlind = e.SmallBlind,
            BigBlind = e.BigBlind,
            Pot = 0,
            CurrentBet = 0,
            CommunityCards = null,
            CurrentStreet = null,
            State = GameState.Dealing
        };
    }

    private GameStateSnapshot ApplyGameStateChanged(GameStateSnapshot state, GameStateChangedEvent e)
    {
        return state with { State = e.NewState };
    }

    private GameStateSnapshot ApplyPlayerJoined(GameStateSnapshot state, PlayerJoinedEvent e)
    {
        var players = state.Players.ToList();
        var existingPlayer = players.FirstOrDefault(p => p.Name == e.PlayerName);

        if (existingPlayer is null)
        {
            players.Add(new ClientPlayerState(
                e.PlayerName,
                players.Count,
                e.ChipStack,
                0,
                false,
                false,
                true,
                false,
                null,
                false,
                false));
        }

        return state with { Players = players };
    }

    private GameStateSnapshot ApplyPlayerLeft(GameStateSnapshot state, PlayerLeftEvent e)
    {
        var players = state.Players.Where(p => p.Name != e.PlayerName).ToList();
        return state with { Players = players };
    }

    private GameStateSnapshot ApplyPlayerConnected(GameStateSnapshot state, PlayerConnectedEvent e)
    {
        var players = state.Players.Select(p => p.Name == e.PlayerName
            ? p with { IsConnected = true }
            : p).ToList();
        return state with { Players = players };
    }

    private GameStateSnapshot ApplyPlayerDisconnected(GameStateSnapshot state, PlayerDisconnectedEvent e)
    {
        var players = state.Players.Select(p => p.Name == e.PlayerName
            ? p with { IsConnected = false }
            : p).ToList();
        return state with { Players = players };
    }

    private GameStateSnapshot ApplyPlayerReconnected(GameStateSnapshot state, PlayerReconnectedEvent e)
    {
        var players = state.Players.Select(p => p.Name == e.PlayerName
            ? p with { IsConnected = true }
            : p).ToList();
        return state with { Players = players };
    }

    private GameStateSnapshot ApplyOptimisticUpdateToState(GameStateSnapshot state, OptimisticUpdate update)
    {
        return update.Type switch
        {
            OptimisticUpdateType.PlayerAction => ApplyOptimisticPlayerAction(state, update),
            OptimisticUpdateType.PlayerFolded => ApplyOptimisticFold(state, update),
            OptimisticUpdateType.ChipStackChange => ApplyOptimisticChipStackChange(state, update),
            OptimisticUpdateType.PlayerTurn => ApplyOptimisticPlayerTurn(state, update),
            _ => state
        };
    }

    private GameStateSnapshot ApplyOptimisticPlayerAction(GameStateSnapshot state, OptimisticUpdate update)
    {
        var players = state.Players.ToList();
        var playerIndex = players.FindIndex(p => p.Name == update.PlayerName);

        if (playerIndex < 0) return state;

        var player = players[playerIndex];
        var amount = update.Amount ?? 0;

        var newPlayer = player with
        {
            ChipStack = update.ActionType switch
            {
                BettingActionType.Bet => player.ChipStack - amount,
                BettingActionType.Call => player.ChipStack - amount,
                BettingActionType.Raise => player.ChipStack - amount,
                BettingActionType.AllIn => 0,
                _ => player.ChipStack
            },
            CurrentBet = update.ActionType switch
            {
                BettingActionType.Bet => player.CurrentBet + amount,
                BettingActionType.Call => player.CurrentBet + amount,
                BettingActionType.Raise => player.CurrentBet + amount,
                BettingActionType.AllIn => player.ChipStack + player.CurrentBet,
                _ => player.CurrentBet
            },
            HasFolded = update.ActionType == BettingActionType.Fold || player.HasFolded,
            IsAllIn = update.ActionType == BettingActionType.AllIn || player.IsAllIn,
            IsCurrentPlayer = false
        };

        players[playerIndex] = newPlayer;

        var newPot = state.Pot + (newPlayer.CurrentBet - player.CurrentBet);

        return state with
        {
            Players = players,
            Pot = newPot,
            CurrentBet = update.ActionType switch
            {
                BettingActionType.Bet => amount,
                BettingActionType.Raise => amount,
                _ => state.CurrentBet
            }
        };
    }

    private GameStateSnapshot ApplyOptimisticFold(GameStateSnapshot state, OptimisticUpdate update)
    {
        var players = state.Players.Select(p => p.Name == update.PlayerName
            ? p with { HasFolded = true, IsCurrentPlayer = false }
            : p).ToList();
        return state with { Players = players };
    }

    private GameStateSnapshot ApplyOptimisticChipStackChange(GameStateSnapshot state, OptimisticUpdate update)
    {
        var players = state.Players.Select(p => p.Name == update.PlayerName
            ? p with { ChipStack = update.Amount ?? p.ChipStack }
            : p).ToList();
        return state with { Players = players };
    }

    private GameStateSnapshot ApplyOptimisticPlayerTurn(GameStateSnapshot state, OptimisticUpdate update)
    {
        var players = state.Players.Select(p => p with
        {
            IsCurrentPlayer = p.Name == update.PlayerName
        }).ToList();

        return state with
        {
            Players = players,
            CurrentPlayerName = update.PlayerName
        };
    }

    private GameStateSnapshot ConvertToGameStateSnapshot(Guid tableId, TableStateSnapshot snapshot, long version)
    {
        var players = snapshot.Players.Select((p, idx) => new ClientPlayerState(
            p.Name,
            p.Position,
            p.ChipStack,
            p.CurrentBet,
            p.HasFolded,
            p.IsAllIn,
            p.IsConnected,
            p.IsAway,
            ConvertVisibleCardsToCardDtos(p.VisibleCards),
            p.Name == snapshot.CurrentPlayerName,
            p.Position == snapshot.DealerPosition
        )).ToList();

        return new GameStateSnapshot(
            tableId,
            snapshot.TableName,
            snapshot.Variant,
            snapshot.State,
            players,
            ConvertVisibleCardsToCardDtos(snapshot.CommunityCards),
            snapshot.DealerPosition,
            snapshot.CurrentPlayerPosition,
            snapshot.CurrentPlayerName,
            snapshot.SmallBlind,
            snapshot.BigBlind,
            snapshot.Pot,
            snapshot.CurrentBet,
            snapshot.CurrentStreet,
            snapshot.HandNumber,
            version,
            DateTime.UtcNow);
    }

    private IReadOnlyList<CardDto>? ConvertVisibleCardsToCardDtos(IReadOnlyList<string>? visibleCards)
    {
        if (visibleCards is null || visibleCards.Count == 0) return null;

        return visibleCards.Select(c => ParseCardString(c)).ToList();
    }

    private static CardDto ParseCardString(string cardString)
    {
        if (string.IsNullOrEmpty(cardString) || cardString.Length < 2)
        {
            return new CardDto("?", "?", cardString);
        }

        // Card strings are in format like "As" (Ace of spades), "2h" (2 of hearts), "10d" (10 of diamonds)
        // The rank is everything except the last character, the suit is the last character
        var rank = cardString.Substring(0, cardString.Length - 1);
        var suit = cardString.Substring(cardString.Length - 1);
        return new CardDto(rank, suit, cardString);
    }

    private void NotifyStateChanged()
    {
        if (_currentState is not null)
        {
            OnStateChanged?.Invoke(_currentState);
        }
    }
}
