using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBettingAction;

public record ProcessBettingActionSuccessful
{
	public Guid GameId { get; init; }
	public bool RoundComplete { get; init; }
	public required string CurrentPhase { get; init; }
	public required BettingActionResult Action { get; init; }
	public int PlayerSeatIndex { get; init; }
	public int NextPlayerIndex { get; init; }
	public string? NextPlayerName { get; init; }
	public int PotTotal { get; init; }
	public int CurrentBet { get; init; }
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
