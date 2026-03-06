using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetCashierLedger;

public sealed record GetCashierLedgerQuery(int PageSize = 10, int PageNumber = 1) : IRequest<CashierLedgerPageDto>;
