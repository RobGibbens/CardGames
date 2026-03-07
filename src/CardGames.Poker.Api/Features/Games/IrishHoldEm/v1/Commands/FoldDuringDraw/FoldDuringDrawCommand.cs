using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.FoldDuringDraw;

/// <summary>
/// Command to fold a player during the Irish Hold 'Em discard phase (e.g., when their timer expires).
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="PlayerSeatIndex">The seat index of the player to fold.</param>
public record FoldDuringDrawCommand(
	Guid GameId,
	int PlayerSeatIndex
) : IRequest<OneOf<FoldDuringDrawSuccessful, FoldDuringDrawError>>, IGameStateChangingCommand;

/// <summary>
/// Successful result of folding during draw phase.
/// </summary>
public record FoldDuringDrawSuccessful : IPlayerActionResult
{
	public required Guid GameId { get; init; }
	public required string PlayerName { get; init; }
	public required int PlayerSeatIndex { get; init; }
	public required string CurrentPhase { get; init; }
	public required bool OnlyOnePlayerRemains { get; init; }

	string? IPlayerActionResult.PlayerName => PlayerName;
	string IPlayerActionResult.ActionDescription => "Folded";
}

/// <summary>
/// Error result of folding during draw phase.
/// </summary>
public record FoldDuringDrawError
{
	public required string Message { get; init; }
}
