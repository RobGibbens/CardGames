using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetCashierLedger;

public sealed record GetCashierLedgerQuery(int Take = 25, int Skip = 0) : IRequest<CashierLedgerPageDto>;
