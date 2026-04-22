using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Profile.v1.Cashier;
using CardGames.Poker.Api.Features.Profile.v1.Queries.GetCashierSummary;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Queries;

public class GetCashierSummaryQueryHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task GetCashierSummary_WhenAuthenticatedUserHasNoPlayerOrAccount_CreatesSeededAccountOnce()
	{
		SetCurrentUser("new-user-id", "New User", "new.user@test.com");

		var firstResult = await Mediator.Send(new GetCashierSummaryQuery());
		var secondResult = await Mediator.Send(new GetCashierSummaryQuery());

		firstResult.CurrentBalance.Should().Be(CashierAccountInitializer.StartingChipAmount);
		firstResult.LastTransactionAtUtc.Should().NotBeNull();
		secondResult.CurrentBalance.Should().Be(CashierAccountInitializer.StartingChipAmount);

		var player = await DbContext.Players.SingleAsync(x => x.Email == "new.user@test.com");
		player.ExternalId.Should().Be("new-user-id");

		var account = await DbContext.PlayerChipAccounts.SingleAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(CashierAccountInitializer.StartingChipAmount);

		var ledgerEntries = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == player.Id)
			.ToListAsync();

		ledgerEntries.Should().ContainSingle();

		var ledgerEntry = ledgerEntries[0];
		ledgerEntry.Type.Should().Be(PlayerChipLedgerEntryType.Add);
		ledgerEntry.ReferenceType.Should().Be(CashierAccountInitializer.RegistrationReferenceType);
		ledgerEntry.AmountDelta.Should().Be(CashierAccountInitializer.StartingChipAmount);
		ledgerEntry.BalanceAfter.Should().Be(CashierAccountInitializer.StartingChipAmount);
	}

	private void SetCurrentUser(string userId, string userName, string userEmail)
	{
		var currentUser = Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		currentUser.Should().BeOfType<FakeCurrentUserService>();

		var fakeCurrentUser = (FakeCurrentUserService)currentUser;
		fakeCurrentUser.UserId = userId;
		fakeCurrentUser.UserName = userName;
		fakeCurrentUser.UserEmail = userEmail;
		fakeCurrentUser.IsAuthenticated = true;
	}
}