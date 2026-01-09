using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;

public record CreateGameCommand(
	Guid GameId,
	string GameCode,
	string? GameName,
	int Ante,
	int MinBet,
	IReadOnlyList<PlayerInfo> Players) : IRequest<OneOf<CreateGameSuccessful, CreateGameConflict>>, IGameStateChangingCommand, ILobbyStateChangingCommand;
