using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands;

/// <summary>
/// Command to deal five cards to each active player in a Seven Card Stud game.
/// </summary>
/// <param name="GameId">The unique identifier of the game to deal cards in.</param>
public record DealHandsCommand(Guid GameId) : IRequest<OneOf<DealHandsSuccessful, DealHandsError>>, IGameStateChangingCommand;
