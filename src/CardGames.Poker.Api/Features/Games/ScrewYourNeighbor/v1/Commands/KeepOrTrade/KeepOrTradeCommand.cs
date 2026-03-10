using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;

/// <summary>
/// Command for a player to make their keep or trade decision in Screw Your Neighbor.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="PlayerId">The unique identifier of the player making the decision.</param>
/// <param name="Decision">The decision: "Keep" or "Trade".</param>
public record KeepOrTradeCommand(
	Guid GameId,
	Guid PlayerId,
	string Decision) : IRequest<OneOf<KeepOrTradeSuccessful, KeepOrTradeError>>, IGameStateChangingCommand;
