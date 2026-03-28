using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.AceChoice;

/// <summary>
/// Command for a player to declare their Ace as high or low in In-Between.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="PlayerId">The unique identifier of the player making the choice.</param>
/// <param name="AceIsHigh">True if the player declares the Ace as high (14), false for low (1).</param>
public record AceChoiceCommand(
	Guid GameId,
	Guid PlayerId,
	bool AceIsHigh) : IRequest<OneOf<AceChoiceSuccessful, AceChoiceError>>, IGameStateChangingCommand;
