using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.PlaceBet;

/// <summary>
/// Command for a player to place a bet (or pass with 0) in In-Between.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="PlayerId">The unique identifier of the player placing the bet.</param>
/// <param name="Amount">The bet amount. 0 = pass.</param>
public record PlaceBetCommand(
	Guid GameId,
	Guid PlayerId,
	int Amount) : IRequest<OneOf<PlaceBetSuccessful, PlaceBetError>>, IGameStateChangingCommand;
