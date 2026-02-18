using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetCashierSummary;

public sealed record GetCashierSummaryQuery : IRequest<CashierSummaryDto>;
