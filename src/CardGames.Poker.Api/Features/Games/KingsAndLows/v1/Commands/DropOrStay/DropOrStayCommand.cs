using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;

/// <summary>
/// Command for a player to make their drop or stay decision in Kings and Lows.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="PlayerId">The unique identifier of the player making the decision.</param>
/// <param name="Decision">The decision: "Drop" or "Stay".</param>
public record DropOrStayCommand(
	Guid GameId,
	Guid PlayerId,
	string Decision) : IRequest<OneOf<DropOrStaySuccessful, DropOrStayError>>, IGameStateChangingCommand;
