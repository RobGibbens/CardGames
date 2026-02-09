using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.ProcessBettingAction;

/// <summary>
/// Represents a successful betting action result.
/// </summary>
public record ProcessBettingActionSuccessful : IPlayerActionResult
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public Guid GameId { get; init; }

	/// <summary>
	/// Indicates whether the betting round is complete.
	/// </summary>
	public bool RoundComplete { get; init; }

	/// <summary>
	/// The current phase of the game after the action.
	/// </summary>
	public required string CurrentPhase { get; init; }

	/// <summary>
	/// The action that was taken.
	/// </summary>
	public required BettingActionResult Action { get; init; }

	/// <summary>
	/// The index of the next player who must act, or -1 if round is complete.
	/// </summary>
	public int NextPlayerIndex { get; init; }

	/// <summary>
	/// The name of the next player who must act, or null if round is complete.
	/// </summary>
	public string? NextPlayerName { get; init; }

	/// <summary>
	/// The current pot total after the action.
	/// </summary>
	public int PotTotal { get; init; }

	/// <summary>
	/// The current bet to match.
	/// </summary>
	public int CurrentBet { get; init; }

	/// <summary>
	/// The seat index of the player who performed the action.
	/// </summary>
	public int PlayerSeatIndex { get; init; }

	/// <inheritdoc />
	string? IPlayerActionResult.PlayerName => Action.PlayerName;

	/// <inheritdoc />
	string IPlayerActionResult.ActionDescription => GetActionDescription();

	private string GetActionDescription()
	{
		return Action.ActionType switch
		{
			BettingActionType.Check => "Checked",
			BettingActionType.Call => $"Called {Action.Amount}",
			BettingActionType.Bet => $"Bet {Action.Amount}",
			BettingActionType.Raise => $"Raised to {Action.Amount}",
			BettingActionType.Fold => "Folded",
			BettingActionType.AllIn => "All In!",
			_ => Action.ActionType.ToString()
		};
	}
}

/// <summary>
/// Represents the details of a betting action taken.
/// </summary>
public record BettingActionResult
{
	/// <summary>
	/// The name of the player who took the action.
	/// </summary>
	public required string PlayerName { get; init; }

	/// <summary>
	/// The type of action taken.
	/// </summary>
	public BettingActionType ActionType { get; init; }

	/// <summary>
	/// The amount involved in the action.
	/// </summary>
	public int Amount { get; init; }

	/// <summary>
	/// The player's chip stack after the action.
	/// </summary>
	public int ChipStackAfter { get; init; }
}

/// <summary>
/// Represents an error when processing a betting action.
/// </summary>
public record ProcessBettingActionError
{
	/// <summary>
	/// The error message describing why the action failed.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The error code for programmatic handling.
	/// </summary>
	public required ProcessBettingActionErrorCode Code { get; init; }
}

/// <summary>
/// Error codes for process betting action failures.
/// </summary>
public enum ProcessBettingActionErrorCode
{
	/// <summary>
	/// The specified game was not found.
	/// </summary>
	GameNotFound,

	/// <summary>
	/// The game is not in a valid state for betting actions.
	/// </summary>
	InvalidGameState,

	/// <summary>
	/// No active betting round exists.
	/// </summary>
	NoBettingRound,

	/// <summary>
	/// It is not the requesting player's turn to act.
	/// </summary>
	NotPlayerTurn,

	/// <summary>
	/// The betting action is invalid (e.g., cannot check when there's a bet).
	/// </summary>
	InvalidAction
}
