using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Context provided to <see cref="IGameFlowHandler.PerformAutoActionAsync"/>.
/// </summary>
/// <param name="GameId">The ID of the game.</param>
/// <param name="PlayerSeatIndex">The seat index of the player to act for, or -1 for global game actions.</param>
/// <param name="CurrentPhase">The current phase of the game.</param>
/// <param name="Game">The loaded game entity (with necessary includes).</param>
/// <param name="DbContext">The database context.</param>
/// <param name="Mediator">The mediator for sending commands.</param>
/// <param name="Logger">Logger for logging actions.</param>
/// <param name="CancellationToken">Cancellation token.</param>
public record AutoActionContext(
    Guid GameId,
    int PlayerSeatIndex,
    string CurrentPhase,
    Game Game,
    CardsDbContext DbContext,
    IMediator Mediator,
    ILogger Logger,
    CancellationToken CancellationToken
);
