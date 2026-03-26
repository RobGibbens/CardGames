using CardGames.Contracts.SignalR;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetPendingJoinRequestsForHost;

public sealed record GetPendingJoinRequestsForHostQuery : IRequest<IReadOnlyList<GameJoinRequestReceivedDto>>;