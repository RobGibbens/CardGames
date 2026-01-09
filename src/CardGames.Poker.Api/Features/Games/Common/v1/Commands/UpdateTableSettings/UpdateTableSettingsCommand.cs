using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.UpdateTableSettings;

/// <summary>
/// Command to update table settings.
/// </summary>
/// <param name="GameId">The unique identifier of the game to update.</param>
/// <param name="Name">The new display name for the table.</param>
/// <param name="Ante">The new ante amount.</param>
/// <param name="MinBet">The new minimum bet amount.</param>
/// <param name="SmallBlind">The new small blind amount.</param>
/// <param name="BigBlind">The new big blind amount.</param>
/// <param name="RowVersion">The concurrency token for optimistic locking.</param>
public record UpdateTableSettingsCommand(
    Guid GameId,
    string? Name,
    int? Ante,
    int? MinBet,
    int? SmallBlind,
    int? BigBlind,
    string RowVersion)
    : IRequest<OneOf<UpdateTableSettingsSuccessful, UpdateTableSettingsError>>, IGameStateChangingCommand;
