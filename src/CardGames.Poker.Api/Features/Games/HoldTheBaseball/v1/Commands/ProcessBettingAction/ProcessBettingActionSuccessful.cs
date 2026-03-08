using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.ProcessBettingAction;

/// <summary>
/// Represents a successful betting action result.
/// </summary>
public record ProcessBettingActionSuccessful : IPlayerActionResult
{
	public Guid GameId { get; init; }
	public bool RoundComplete { get; init; }
	public required string CurrentPhase { get; init; }
	public required BettingActionResult Action { get; init; }
	public int NextPlayerIndex { get; init; }
	public string? NextPlayerName { get; init; }
	public int PotTotal { get; init; }
	public int CurrentBet { get; init; }
	public int PlayerSeatIndex { get; init; }

	string? IPlayerActionResult.PlayerName => Action.PlayerName;

	string IPlayerActionResult.ActionDescription => Action.ActionType switch
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

public record BettingActionResult
{
	public required string PlayerName { get; init; }
	public BettingActionType ActionType { get; init; }
	public int Amount { get; init; }
	public int ChipStackAfter { get; init; }
}

public record ProcessBettingActionError
{
	public required string Message { get; init; }
	public required ProcessBettingActionErrorCode Code { get; init; }
}

public enum ProcessBettingActionErrorCode
{
	GameNotFound,
	InvalidGameState,
	NoBettingRound,
	NotPlayerTurn,
	InvalidAction
}
