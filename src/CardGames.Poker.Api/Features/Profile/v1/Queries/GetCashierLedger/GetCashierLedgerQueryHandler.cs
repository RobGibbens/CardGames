using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Features.Profile.v1.Cashier;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetCashierLedger;

public sealed class GetCashierLedgerQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetCashierLedgerQuery, CashierLedgerPageDto>
{
	public async Task<CashierLedgerPageDto> Handle(GetCashierLedgerQuery request, CancellationToken cancellationToken)
	{
		var take = Math.Clamp(request.Take, 1, 100);
		var skip = Math.Max(request.Skip, 0);

		var player = await CashierPlayerResolver.TryResolveAsync(context, currentUserService, cancellationToken);
		if (player is null)
		{
			return new CashierLedgerPageDto
			{
				Entries = [],
				HasMore = false,
				TotalCount = 0
			};
		}

		var totalCount = await context.PlayerChipLedgerEntries
			.AsNoTracking()
			.Where(x => x.PlayerId == player.Id)
			.CountAsync(cancellationToken);

		var entries = await context.PlayerChipLedgerEntries
			.AsNoTracking()
			.Where(x => x.PlayerId == player.Id)
			.OrderByDescending(x => x.OccurredAtUtc)
			.ThenByDescending(x => x.Id)
			.Skip(skip)
			.Take(take)
			.Select(x => new CashierLedgerEntryDto
			{
				Id = x.Id,
				OccurredAtUtc = x.OccurredAtUtc,
				Type = x.Type.ToString(),
				AmountDelta = x.AmountDelta,
				BalanceAfter = x.BalanceAfter,
				ReferenceType = x.ReferenceType,
				ReferenceId = x.ReferenceId,
				Reason = x.Reason
			})
			.ToListAsync(cancellationToken);

		return new CashierLedgerPageDto
		{
			Entries = entries,
			HasMore = skip + entries.Count < totalCount,
			TotalCount = totalCount
		};
	}
}
