using CardGames.Contracts.AddChips;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

/// <summary>
/// Command to add chips to a player's stack in a game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="PlayerId">The player's unique identifier.</param>
/// <param name="Amount">The amount of chips to add (must be positive).</param>
public record AddChipsCommand(Guid GameId, Guid PlayerId, int Amount)
	: IRequest<OneOf<AddChipsResponse, AddChipsError>>, IGameStateChangingCommand;
