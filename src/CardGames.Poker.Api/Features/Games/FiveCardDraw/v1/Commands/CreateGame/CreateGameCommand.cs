using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CreateGame;

public record CreateGameCommand(
	string? GameName,
	int Ante,
	int MinBet,
	IReadOnlyList<PlayerInfo> Players) : IRequest<OneOf<CreateGameSuccessful>>;