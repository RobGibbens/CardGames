using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.AcknowledgePotMatch;

/// <summary>
/// Command to process pot matching in Kings and Lows after showdown.
/// Losers must match the pot amount.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
public record AcknowledgePotMatchCommand(Guid GameId) : IRequest<OneOf<AcknowledgePotMatchSuccessful, AcknowledgePotMatchError>>, IGameStateChangingCommand;
