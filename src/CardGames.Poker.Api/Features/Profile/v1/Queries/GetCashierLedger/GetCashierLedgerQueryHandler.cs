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
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
		var pageNumber = Math.Max(request.PageNumber, 1);

		var player = await CashierPlayerResolver.ResolveOrCreateAsync(context, currentUserService, cancellationToken);
		if (player is null)
		{
			return new CashierLedgerPageDto
			{
				Entries = [],
				HasMore = false,
              TotalCount = 0,
				PageNumber = 1,
				PageSize = pageSize,
				TotalPages = 1
			};
		}

		await CashierAccountInitializer.EnsureRegistrationCreditAsync(
			context,
			player.Id,
			currentUserService.UserId,
			cancellationToken);

		if (context.ChangeTracker.HasChanges())
		{
			await context.SaveChangesAsync(cancellationToken);
		}

		var totalCount = await context.PlayerChipLedgerEntries
			.AsNoTracking()
			.Where(x => x.PlayerId == player.Id)
			.CountAsync(cancellationToken);

     var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
		if (pageNumber > totalPages)
		{
			pageNumber = totalPages;
		}

		var skip = (pageNumber - 1) * pageSize;

		var entries = await context.PlayerChipLedgerEntries
			.AsNoTracking()
			.Where(x => x.PlayerId == player.Id)
			.OrderByDescending(x => x.OccurredAtUtc)
			.ThenByDescending(x => x.Id)
			.Skip(skip)
         .Take(pageSize)
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
            HasMore = pageNumber < totalPages,
			TotalCount = totalCount,
			PageNumber = pageNumber,
			PageSize = pageSize,
			TotalPages = totalPages
		};
	}
}
