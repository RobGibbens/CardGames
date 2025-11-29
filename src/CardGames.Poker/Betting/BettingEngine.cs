using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Betting;

/// <summary>
/// Manages betting rounds with server-side enforcement of betting rules.
/// Supports different limit types (No Limit, Pot Limit, Fixed Limit) and turn management.
/// </summary>
public class BettingEngine : IBettingEngine, IDisposable
{
    private readonly List<PokerPlayer> _players;
    private readonly PotManager _potManager;
    private readonly int _bigBlind;
    private readonly TurnTimerConfig _timerConfig;
    private readonly List<BettingAction> _currentRoundActions = [];

    private ILimitStrategy _limitStrategy;
    private TurnTimer _turnTimer;
    private int _currentBet;
    private int _lastRaiseAmount;
    private int _currentPlayerIndex;
    private int _lastAggressorIndex = -1;
    private HashSet<int> _playersWhoActed = [];
    private bool _roundInProgress;
    private bool _roundComplete;
    private string _currentRoundName = "";
    private bool _disposed;

    /// <inheritdoc />
    public bool IsRoundInProgress => _roundInProgress;

    /// <inheritdoc />
    public bool IsRoundComplete => _roundComplete;

    /// <inheritdoc />
    public PokerPlayer CurrentPlayer => _players.Count > 0 && _currentPlayerIndex >= 0 && _currentPlayerIndex < _players.Count 
        ? _players[_currentPlayerIndex] 
        : null;

    /// <inheritdoc />
    public int CurrentBet => _currentBet;

    /// <inheritdoc />
    public int TotalPot => _potManager.TotalPotAmount;

    /// <inheritdoc />
    public IReadOnlyList<BettingAction> CurrentRoundActions => _currentRoundActions.AsReadOnly();

    /// <inheritdoc />
    public ILimitStrategy LimitStrategy => _limitStrategy;

    /// <inheritdoc />
    public int PlayersInHand => _players.Count(p => !p.HasFolded);

    /// <inheritdoc />
    public int ActivePlayers => _players.Count(p => p.CanAct);

    /// <inheritdoc />
    public event Action<BettingEngineEvent> OnEvent;

    /// <summary>
    /// Creates a new betting engine.
    /// </summary>
    /// <param name="players">The list of players in the game.</param>
    /// <param name="potManager">The pot manager for tracking contributions.</param>
    /// <param name="limitStrategy">The limit strategy to use.</param>
    /// <param name="bigBlind">The big blind amount.</param>
    /// <param name="timerConfig">Optional timer configuration.</param>
    public BettingEngine(
        List<PokerPlayer> players,
        PotManager potManager,
        ILimitStrategy limitStrategy,
        int bigBlind,
        TurnTimerConfig timerConfig = null)
    {
        _players = players ?? throw new ArgumentNullException(nameof(players));
        _potManager = potManager ?? throw new ArgumentNullException(nameof(potManager));
        _limitStrategy = limitStrategy ?? throw new ArgumentNullException(nameof(limitStrategy));
        _bigBlind = bigBlind;
        _timerConfig = timerConfig ?? TurnTimerConfig.CashGame;

        if (_timerConfig.DefaultTimeoutSeconds > 0)
        {
            _turnTimer = new TurnTimer(_timerConfig.DefaultTimeoutSeconds, _timerConfig.WarningThresholdSeconds);
            _turnTimer.OnTimeout += HandleTimeout;
            _turnTimer.OnWarning += HandleWarning;
        }
    }

    /// <summary>
    /// Sets the limit strategy.
    /// </summary>
    /// <param name="strategy">The new limit strategy to use.</param>
    public void SetLimitStrategy(ILimitStrategy strategy)
    {
        _limitStrategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    }

    /// <inheritdoc />
    public void StartRound(string roundName, int initialBet = 0, int forcedBetPlayerIndex = -1)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BettingEngine));
        }

        _currentRoundName = roundName;
        _currentRoundActions.Clear();
        _playersWhoActed.Clear();
        _currentBet = initialBet;
        _lastRaiseAmount = _bigBlind;
        _roundComplete = false;
        _roundInProgress = true;
        _lastAggressorIndex = -1;

        // Track the forced bet player as having acted
        if (forcedBetPlayerIndex >= 0 && forcedBetPlayerIndex < _players.Count)
        {
            _playersWhoActed.Add(forcedBetPlayerIndex);
            _lastAggressorIndex = forcedBetPlayerIndex;
        }

        // Find first active player
        _currentPlayerIndex = FindFirstActivePlayer(forcedBetPlayerIndex >= 0 
            ? (forcedBetPlayerIndex + 1) % _players.Count 
            : 0);

        CheckIfRoundComplete();

        if (!_roundComplete)
        {
            var activePlayers = _players.Where(p => !p.HasFolded).Select(p => p.Name).ToList();
            RaiseEvent(new BettingRoundStartedEvent(roundName, TotalPot, activePlayers));
            StartTurn();
        }
    }

    /// <inheritdoc />
    public AvailableActions GetAvailableActions()
    {
        if (CurrentPlayer == null)
        {
            return null;
        }
        return GetAvailableActionsForPlayer(CurrentPlayer);
    }

    /// <inheritdoc />
    public AvailableActions GetAvailableActionsForPlayer(PokerPlayer player)
    {
        if (player == null)
        {
            return null;
        }

        var amountToCall = player.AmountToCall(_currentBet);
        var canAffordCall = player.ChipStack >= amountToCall;
        var minBet = _limitStrategy.GetMinBet(_bigBlind, _currentBet, _lastRaiseAmount);
        var maxBet = _limitStrategy.GetMaxBet(player.ChipStack, TotalPot, _currentBet, player.CurrentBet);
        var minRaise = _limitStrategy.GetMinRaise(_currentBet, _lastRaiseAmount, _bigBlind);
        var maxRaise = _limitStrategy.GetMaxRaise(player.ChipStack, TotalPot, _currentBet, player.CurrentBet);

        return new AvailableActions
        {
            CanCheck = _currentBet == player.CurrentBet,
            CanBet = _currentBet == 0 && player.ChipStack >= minBet,
            CanCall = _currentBet > player.CurrentBet && canAffordCall && amountToCall < player.ChipStack,
            CanRaise = _currentBet > 0 && player.ChipStack > amountToCall,
            CanFold = _currentBet > player.CurrentBet,
            CanAllIn = player.ChipStack > 0,
            MinBet = minBet,
            MaxBet = maxBet,
            CallAmount = Math.Min(amountToCall, player.ChipStack),
            MinRaise = minRaise
        };
    }

    /// <inheritdoc />
    public bool IsValidAction(BettingActionType actionType, int amount = 0)
    {
        var player = CurrentPlayer;
        if (player == null)
        {
            return false;
        }

        var available = GetAvailableActions();
        return ValidateAction(actionType, amount, available, player) == null;
    }

    /// <inheritdoc />
    public BettingEngineResult ProcessAction(BettingActionType actionType, int amount = 0)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BettingEngine));
        }

        if (!_roundInProgress || _roundComplete)
        {
            return BettingEngineResult.Failed("No active betting round");
        }

        var player = CurrentPlayer;
        if (player == null)
        {
            return BettingEngineResult.Failed("No current player");
        }

        var available = GetAvailableActions();
        var validationError = ValidateAction(actionType, amount, available, player);

        if (validationError != null)
        {
            RaiseEvent(new InvalidActionEvent(player.Name, actionType, amount, validationError));
            return BettingEngineResult.Failed(validationError);
        }

        // Stop the turn timer
        _turnTimer?.Stop();

        // Execute the action
        var actualAmount = ExecuteAction(player, actionType, amount, available);
        var action = new BettingAction(player.Name, actionType, actualAmount);
        _currentRoundActions.Add(action);

        // Track that this player has acted
        _playersWhoActed.Add(_currentPlayerIndex);

        // Check for hand completion (only one player remaining)
        var handComplete = PlayersInHand <= 1;

        // Move to next player and check round completion
        MoveToNextPlayer();
        CheckIfRoundComplete();

        var result = BettingEngineResult.Successful(
            action,
            TotalPot,
            _roundComplete,
            handComplete);

        RaiseEvent(new ActionTakenEvent(action, TotalPot, _roundComplete));

        if (_roundComplete)
        {
            _roundInProgress = false;
            RaiseEvent(new BettingRoundCompletedEvent(
                _currentRoundName,
                TotalPot,
                _currentRoundActions.ToList(),
                handComplete));
        }
        else
        {
            StartTurn();
        }

        return result;
    }

    /// <inheritdoc />
    public BettingEngineResult ProcessDefaultAction()
    {
        var available = GetAvailableActions();
        if (available == null)
        {
            return BettingEngineResult.Failed("No current player");
        }

        // Default action: check if possible, otherwise fold
        if (available.CanCheck)
        {
            return ProcessAction(BettingActionType.Check);
        }
        else
        {
            return ProcessAction(BettingActionType.Fold);
        }
    }

    /// <inheritdoc />
    public void ResetPlayerBets()
    {
        foreach (var player in _players)
        {
            player.ResetCurrentBet();
        }
    }

    private string ValidateAction(BettingActionType actionType, int amount, AvailableActions available, PokerPlayer player)
    {
        return actionType switch
        {
            BettingActionType.Check when !available.CanCheck => "Cannot check - there is a bet to match",
            BettingActionType.Bet when !available.CanBet => "Cannot bet - betting is not available",
            BettingActionType.Bet when !_limitStrategy.IsValidBet(amount, _bigBlind, _currentBet, _lastRaiseAmount, player.ChipStack, TotalPot, player.CurrentBet) =>
                $"Invalid bet amount. Min: {available.MinBet}, Max: {available.MaxBet}",
            BettingActionType.Call when _currentBet == 0 => "Cannot call - no bet to match",
            BettingActionType.Call when !available.CanCall && !available.CanAllIn => "Cannot call - insufficient chips",
            BettingActionType.Raise when !available.CanRaise => "Cannot raise - raising is not available",
            BettingActionType.Raise when !_limitStrategy.IsValidRaise(amount, _currentBet, _lastRaiseAmount, _bigBlind, player.ChipStack, TotalPot, player.CurrentBet) =>
                $"Invalid raise amount. Min: {available.MinRaise}",
            BettingActionType.Fold when !available.CanFold && available.CanCheck => "Cannot fold when you can check",
            BettingActionType.AllIn when !available.CanAllIn => "Cannot go all-in - no chips remaining",
            _ => null
        };
    }

    private int ExecuteAction(PokerPlayer player, BettingActionType actionType, int amount, AvailableActions available)
    {
        int actualAmount = 0;

        switch (actionType)
        {
            case BettingActionType.Check:
                break;

            case BettingActionType.Bet:
                actualAmount = player.PlaceBet(amount);
                _currentBet = player.CurrentBet;
                _lastRaiseAmount = amount;
                _lastAggressorIndex = _currentPlayerIndex;
                _playersWhoActed.Clear();
                _playersWhoActed.Add(_currentPlayerIndex);
                _potManager.AddContribution(player.Name, actualAmount);
                break;

            case BettingActionType.Call:
                var callAmount = player.AmountToCall(_currentBet);
                actualAmount = player.PlaceBet(callAmount);
                _potManager.AddContribution(player.Name, actualAmount);
                break;

            case BettingActionType.Raise:
                var raiseContribution = amount - player.CurrentBet;
                actualAmount = player.PlaceBet(raiseContribution);
                _lastRaiseAmount = player.CurrentBet - _currentBet;
                _currentBet = player.CurrentBet;
                _lastAggressorIndex = _currentPlayerIndex;
                _playersWhoActed.Clear();
                _playersWhoActed.Add(_currentPlayerIndex);
                _potManager.AddContribution(player.Name, actualAmount);
                break;

            case BettingActionType.Fold:
                player.Fold();
                _potManager.RemovePlayerEligibility(player.Name);
                break;

            case BettingActionType.AllIn:
                var allInAmount = player.ChipStack;
                actualAmount = player.PlaceBet(allInAmount);
                if (player.CurrentBet > _currentBet)
                {
                    _lastRaiseAmount = player.CurrentBet - _currentBet;
                    _currentBet = player.CurrentBet;
                    _lastAggressorIndex = _currentPlayerIndex;
                    _playersWhoActed.Clear();
                    _playersWhoActed.Add(_currentPlayerIndex);
                }
                _potManager.AddContribution(player.Name, actualAmount);
                break;
        }

        return actualAmount;
    }

    private void MoveToNextPlayer()
    {
        _currentPlayerIndex = FindNextActivePlayer((_currentPlayerIndex + 1) % _players.Count);
    }

    private int FindFirstActivePlayer(int startIndex)
    {
        return FindNextActivePlayer(startIndex);
    }

    private int FindNextActivePlayer(int startIndex)
    {
        var index = startIndex;
        var count = 0;
        while (count < _players.Count)
        {
            if (_players[index].CanAct)
            {
                return index;
            }
            index = (index + 1) % _players.Count;
            count++;
        }
        return startIndex;
    }

    private void CheckIfRoundComplete()
    {
        var activePlayersCount = _players.Count(p => p.CanAct);
        var playersInHand = _players.Count(p => !p.HasFolded);

        // Only one player remaining - hand is complete
        if (playersInHand <= 1)
        {
            _roundComplete = true;
            return;
        }

        // Everyone is all-in or folded
        if (activePlayersCount == 0)
        {
            _roundComplete = true;
            return;
        }

        // All players who can act have acted and matched the current bet
        var allActed = _players
            .Select((p, i) => (player: p, index: i))
            .Where(x => x.player.CanAct)
            .All(x => _playersWhoActed.Contains(x.index) && x.player.CurrentBet == _currentBet);

        // Special case: if everyone has checked (no bet made and all active players acted)
        if (_currentBet == 0)
        {
            var allChecked = _players
                .Select((p, i) => (player: p, index: i))
                .Where(x => x.player.CanAct)
                .All(x => _playersWhoActed.Contains(x.index));
            _roundComplete = allChecked;
            return;
        }

        _roundComplete = allActed;
    }

    private void StartTurn()
    {
        var player = CurrentPlayer;
        if (player == null || _roundComplete)
        {
            return;
        }

        var available = GetAvailableActions();
        var timeout = _timerConfig?.DefaultTimeoutSeconds ?? 0;

        RaiseEvent(new TurnStartedEvent(player.Name, available, timeout));

        if (_turnTimer != null && timeout > 0)
        {
            _turnTimer.Start();
        }
    }

    private void HandleTimeout()
    {
        if (!_roundInProgress || _roundComplete)
        {
            return;
        }

        var player = CurrentPlayer;
        if (player == null)
        {
            return;
        }

        if (_timerConfig?.AutoActOnTimeout == true)
        {
            var defaultAction = GetDefaultAction();
            RaiseEvent(new TurnTimedOutEvent(player.Name, defaultAction));
            ProcessDefaultAction();
        }
        else
        {
            RaiseEvent(new TurnTimedOutEvent(player.Name, null));
        }
    }

    private void HandleWarning(int secondsRemaining)
    {
        var player = CurrentPlayer;
        if (player != null)
        {
            RaiseEvent(new TurnWarningEvent(player.Name, secondsRemaining));
        }
    }

    private BettingAction GetDefaultAction()
    {
        var player = CurrentPlayer;
        if (player == null)
        {
            return null;
        }

        var available = GetAvailableActions();
        if (available.CanCheck)
        {
            return new BettingAction(player.Name, BettingActionType.Check);
        }
        return new BettingAction(player.Name, BettingActionType.Fold);
    }

    private void RaiseEvent(BettingEngineEvent evt)
    {
        OnEvent?.Invoke(evt);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _turnTimer?.Dispose();
            _disposed = true;
        }
    }
}
