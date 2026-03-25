using MediatR;
using SharedGetCurrentPlayerTurnQuery = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetCurrentPlayerTurn.GetCurrentPlayerTurnQuery;
using SharedGetCurrentPlayerTurnResponse = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetCurrentPlayerTurn.GetCurrentPlayerTurnResponse;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Queries.GetCurrentPlayerTurn;

public sealed class GetCurrentPlayerTurnQueryHandler(
	IRequestHandler<SharedGetCurrentPlayerTurnQuery, SharedGetCurrentPlayerTurnResponse?> innerHandler)
	: IRequestHandler<GetCurrentPlayerTurnQuery, SharedGetCurrentPlayerTurnResponse?>
{
	public Task<SharedGetCurrentPlayerTurnResponse?> Handle(
		GetCurrentPlayerTurnQuery request,
		CancellationToken cancellationToken)
		=> innerHandler.Handle(new SharedGetCurrentPlayerTurnQuery(request.GameId), cancellationToken);
}
