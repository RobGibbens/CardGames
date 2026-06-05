using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Betting;

/// <summary>
///     Manages a single betting round in poker.
/// </summary>
public class BettingRound
{
	private readonly List<BettingAction> _actions = [];
	private readonly int _minBet;
	private readonly List<PokerPlayer> _players;
	private readonly HashSet<int> _playersWhoActed = [];
	private readonly PotManager _potManager;

	private int _currentPlayerIndex;
	private int _lastAggressorIndex = -1;
	private int _lastRaiseAmount;

	/// <summary>
	///     Creates a betting round with an initial forced bet (e.g., bring-in).
	/// </summary>
	public BettingRound(
		List<PokerPlayer> players,
		PotManager potManager,
		int dealerPosition,
		int minBet,
		int initialBet = 0,
		int forcedBetPlayerIndex = -1)
	{
		_players = players;
		_potManager = potManager;
		_minBet = minBet;
		CurrentBet = initialBet;
		_lastRaiseAmount = minBet;

		// Track the forced bet player as having acted
		if (forcedBetPlayerIndex >= 0)
		{
			_playersWhoActed.Add(forcedBetPlayerIndex);
			_lastAggressorIndex = forcedBetPlayerIndex;
		}

		// Action starts with first active player to the left of the dealer/forced bet player
		_currentPlayerIndex = FindNextActivePlayer((dealerPosition + 1) % players.Count);

		CheckIfRoundComplete();
	}

	public int CurrentBet { get; private set; }

	public bool IsComplete { get; private set; }

	public IReadOnlyList<BettingAction> Actions => _actions.AsReadOnly();
	public PokerPlayer CurrentPlayer => _players[_currentPlayerIndex];

	/// <summary>
	///     Returns the number of players still in the hand (not folded).
	/// </summary>
	public int PlayersInHand => _players.Count(p => !p.HasFolded);

	/// <summary>
	///     Gets the available actions for the current player.
	/// </summary>
	public AvailableActions GetAvailableActions()
	{
		var player = _players[_currentPlayerIndex];
		var amountToCall = player.AmountToCall(CurrentBet);
		var canAffordCall = player.ChipStack >= amountToCall;

		return new AvailableActions
		{
			CanCheck = CurrentBet == player.CurrentBet,
			CanBet = CurrentBet == 0 && player.ChipStack >= _minBet,
			CanCall = CurrentBet > player.CurrentBet && canAffordCall && amountToCall < player.ChipStack,
			CanRaise = CurrentBet > 0 && player.ChipStack > amountToCall,
			CanFold = CurrentBet > player.CurrentBet,
			CanAllIn = player.ChipStack > 0,
			MinBet = _minBet,
			MaxBet = player.ChipStack,
			CallAmount = Math.Min(amountToCall, player.ChipStack),
			MinRaise = CurrentBet + _lastRaiseAmount
		};
	}

	/// <summary>
	///     Processes a betting action from the current player.
	/// </summary>
	public BettingRoundResult ProcessAction(BettingActionType actionType, int amount = 0)
	{
		var player = _players[_currentPlayerIndex];
		var available = GetAvailableActions();

		// Validate the action
		var validationError = ValidateAction(actionType, amount, available, player);
		if (validationError != null)
		{
			return new BettingRoundResult
			{
				Success = false,
				ErrorMessage = validationError,
				RoundComplete = false
			};
		}

		// Execute the action
		var actualAmount = ExecuteAction(player, actionType, amount, available);
		var action = new BettingAction(player.Name, actionType, actualAmount);
		_actions.Add(action);

		// Track that this player has acted
		_playersWhoActed.Add(_currentPlayerIndex);

		// Move to next player
		MoveToNextPlayer();
		CheckIfRoundComplete();

		return new BettingRoundResult
		{
			Success = true,
			Action = action,
			RoundComplete = IsComplete
		};
	}

	private string ValidateAction(BettingActionType actionType, int amount, AvailableActions available,
		PokerPlayer player)
	{
		return actionType switch
		{
			BettingActionType.Check when !available.CanCheck => "Cannot check - there is a bet to match",
			BettingActionType.Bet when !available.CanBet => "Cannot bet - betting is not available",
			BettingActionType.Bet when amount < available.MinBet => $"Bet must be at least {available.MinBet}",
			BettingActionType.Bet when amount > available.MaxBet =>
				$"Cannot bet more than your stack ({available.MaxBet})",
			BettingActionType.Call when !available.CanCall && !available.CanAllIn =>
				"Cannot call - no bet to match or insufficient chips",
			BettingActionType.Raise when !available.CanRaise => "Cannot raise - raising is not available",
			BettingActionType.Raise when amount < available.MinRaise && amount < player.ChipStack + player.CurrentBet =>
				$"Raise must be to at least {available.MinRaise}",
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
				// No chips change hands
				break;

			case BettingActionType.Bet:
				actualAmount = player.PlaceBet(amount);
				CurrentBet = player.CurrentBet;
				_lastRaiseAmount = amount;
				_lastAggressorIndex = _currentPlayerIndex;
				_playersWhoActed.Clear();
				_playersWhoActed.Add(_currentPlayerIndex);
				_potManager.AddContribution(player.Name, actualAmount);
				break;

			case BettingActionType.Call:
				var callAmount = player.AmountToCall(CurrentBet);
				actualAmount = player.PlaceBet(callAmount);
				_potManager.AddContribution(player.Name, actualAmount);
				break;

			case BettingActionType.Raise:
				var raiseAmount = amount - player.CurrentBet;
				actualAmount = player.PlaceBet(raiseAmount);
				_lastRaiseAmount = player.CurrentBet - CurrentBet;
				CurrentBet = player.CurrentBet;
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
				if (player.CurrentBet > CurrentBet)
				{
					_lastRaiseAmount = player.CurrentBet - CurrentBet;
					CurrentBet = player.CurrentBet;
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
		var startIndex = _currentPlayerIndex;
		_currentPlayerIndex = FindNextActivePlayer((_currentPlayerIndex + 1) % _players.Count);
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
		// Round is complete if:
		// 1. Only one active player remains (everyone else folded or is all-in)
		var activePlayersCount = _players.Count(p => p.CanAct);
		var playersInHand = _players.Count(p => !p.HasFolded);

		if (playersInHand <= 1)
		{
			IsComplete = true;
			return;
		}

		if (activePlayersCount == 0)
		{
			// Everyone is all-in or folded
			IsComplete = true;
			return;
		}

		// 2. All players who can act have acted and matched the current bet
		var allActed = _players
			.Select((p, i) => (player: p, index: i))
			.Where(x => x.player.CanAct)
			.All(x => _playersWhoActed.Contains(x.index) && x.player.CurrentBet == CurrentBet);

		// Special case: if everyone has checked (no bet made and all active players acted)
		if (CurrentBet == 0)
		{
			var allChecked = _players
				.Select((p, i) => (player: p, index: i))
				.Where(x => x.player.CanAct)
				.All(x => _playersWhoActed.Contains(x.index));
			IsComplete = allChecked;
			return;
		}

		IsComplete = allActed;
	}
}

/// <summary>
///     Result of processing a betting action.
/// </summary>
public class BettingRoundResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public BettingAction Action { get; init; }
	public bool RoundComplete { get; init; }
}