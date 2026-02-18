using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Profile.v1.Cashier;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.AddAccountChips;

public sealed class AddAccountChipsCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	ILogger<AddAccountChipsCommandHandler> logger)
	: IRequestHandler<AddAccountChipsCommand, OneOf<AddAccountChipsResponse, AddAccountChipsError>>
{
	public async Task<OneOf<AddAccountChipsResponse, AddAccountChipsError>> Handle(AddAccountChipsCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated)
		{
			return new AddAccountChipsError(AddAccountChipsErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (request.Amount <= 0)
		{
			return new AddAccountChipsError(AddAccountChipsErrorCode.InvalidAmount, "Amount must be greater than 0.");
		}

		if (request.Amount > 1_000_000)
		{
			return new AddAccountChipsError(AddAccountChipsErrorCode.InvalidAmount, "Amount exceeds the maximum allowed per request.");
		}

		var player = await CashierPlayerResolver.ResolveOrCreateAsync(context, currentUserService, cancellationToken);
		if (player is null)
		{
			return new AddAccountChipsError(AddAccountChipsErrorCode.PlayerUnavailable, "Unable to resolve player account for current user.");
		}

		var now = DateTimeOffset.UtcNow;

		var account = await context.PlayerChipAccounts
			.FirstOrDefaultAsync(x => x.PlayerId == player.Id, cancellationToken);

		if (account is null)
		{
			account = new PlayerChipAccount
			{
				PlayerId = player.Id,
				Balance = 0,
				CreatedAtUtc = now,
				UpdatedAtUtc = now
			};

			context.PlayerChipAccounts.Add(account);
		}

		account.Balance += request.Amount;
		account.UpdatedAtUtc = now;

		var transaction = new PlayerChipLedgerEntry
		{
			Id = Guid.CreateVersion7(),
			PlayerId = player.Id,
			Type = PlayerChipLedgerEntryType.Add,
			AmountDelta = request.Amount,
			BalanceAfter = account.Balance,
			OccurredAtUtc = now,
			ReferenceType = "Cashier",
			ReferenceId = null,
			Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
			ActorUserId = currentUserService.UserId
		};

		context.PlayerChipLedgerEntries.Add(transaction);

		await context.SaveChangesAsync(cancellationToken);

		logger.LogInformation(
			"Added {Amount} account chips for PlayerId={PlayerId}. New balance={NewBalance}, TransactionId={TransactionId}",
			request.Amount,
			player.Id,
			account.Balance,
			transaction.Id);

		return new AddAccountChipsResponse
		{
			NewBalance = account.Balance,
			AppliedAmount = request.Amount,
			TransactionId = transaction.Id,
			Message = $"{request.Amount:N0} chips added to your account."
		};
	}
}
