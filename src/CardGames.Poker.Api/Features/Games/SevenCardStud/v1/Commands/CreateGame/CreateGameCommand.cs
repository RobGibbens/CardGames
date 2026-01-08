using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.CreateGame;

public record CreateGameCommand(
	Guid GameId,
	string? GameName,
	int Ante,
	int BringIn,
	int SmallBet,
	int BigBet,
	IReadOnlyList<PlayerInfo> Players) : IRequest<OneOf<CreateGameSuccessful, CreateGameConflict>>, IGameStateChangingCommand, ILobbyStateChangingCommand;
