using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.ResumeAfterChipCheck;

/// <summary>
/// Command to resume the game after the chip check pause.
/// This can be called when all players have added sufficient chips, or
/// it will be called automatically by the background service when the timer expires.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="ForceResume">If true, resume immediately even if some players are still short on chips (they will auto-drop).</param>
public record ResumeAfterChipCheckCommand(Guid GameId, bool ForceResume = false) 
	: IRequest<OneOf<ResumeAfterChipCheckSuccessful, ResumeAfterChipCheckError>>, IGameStateChangingCommand;
