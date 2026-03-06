using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;

/// <summary>
/// Command issued by the Dealer's Choice dealer to select the game type for the current hand.
/// </summary>
/// <param name="GameId">The game session identifier.</param>
/// <param name="GameTypeCode">The chosen game type code (e.g., "FIVECARDDRAW").</param>
/// <param name="Ante">Ante for this hand.</param>
/// <param name="MinBet">Minimum bet for this hand.</param>
public record ChooseDealerGameCommand(
	Guid GameId,
	string GameTypeCode,
	int Ante,
	int MinBet,
	int? SmallBlind = null,
	int? BigBlind = null) : IRequest<OneOf<ChooseDealerGameSuccessful, ChooseDealerGameError>>, IGameStateChangingCommand;
