using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Features.Profile.v1.Cashier;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetCashierSummary;

public sealed class GetCashierSummaryQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetCashierSummaryQuery, CashierSummaryDto>
{
	public async Task<CashierSummaryDto> Handle(GetCashierSummaryQuery request, CancellationToken cancellationToken)
	{
		var player = await CashierPlayerResolver.ResolveOrCreateAsync(context, currentUserService, cancellationToken);
		if (player is null)
		{
			return new CashierSummaryDto
			{
				CurrentBalance = 0,
				PendingBalanceChange = 0,
				LastTransactionAtUtc = null
			};
		}

		var account = await CashierAccountInitializer.EnsureRegistrationCreditAsync(
			context,
			player.Id,
			currentUserService.UserId,
			cancellationToken);

		if (context.ChangeTracker.HasChanges())
		{
			await context.SaveChangesAsync(cancellationToken);
		}

		var lastTransactionAtUtc = await context.PlayerChipLedgerEntries
			.AsNoTracking()
			.Where(x => x.PlayerId == player.Id)
			.OrderByDescending(x => x.OccurredAtUtc)
			.Select(x => (DateTimeOffset?)x.OccurredAtUtc)
			.FirstOrDefaultAsync(cancellationToken);

		return new CashierSummaryDto
		{
			CurrentBalance = account.Balance,
			PendingBalanceChange = 0,
			LastTransactionAtUtc = lastTransactionAtUtc
		};
	}
}
