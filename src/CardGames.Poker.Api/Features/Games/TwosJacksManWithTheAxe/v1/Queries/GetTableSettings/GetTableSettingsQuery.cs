using MediatR;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetTableSettings;

/// <summary>
/// Query to retrieve table settings for a game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
public record GetTableSettingsQuery(Guid GameId) : IRequest<GetTableSettingsResponse?>;
