namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;

/// <summary>
/// Errors that can occur when leaving a game.
/// </summary>
/// <param name="Message">A human-readable error message.</param>
public sealed record LeaveGameError(string Message);
