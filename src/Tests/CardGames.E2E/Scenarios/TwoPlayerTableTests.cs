using CardGames.E2E.Infrastructure;

namespace CardGames.E2E.Scenarios;

[Collection(PlaywrightE2ECollection.Name)]
public sealed class TwoPlayerTableTests : E2ETestBase
{
	public TwoPlayerTableTests(PlaywrightAppFixture fixture)
		: base(fixture)
	{
	}

	[E2EFact]
	public async Task SeatedPlayers_SeeEachOther_WithoutReload()
	{
		await ExecuteAsync(async () =>
		{
			var avery = await Seeder.CreatePlayerAsync("player-a@e2e.test", "Avery", "One");
			var blake = await Seeder.CreatePlayerAsync("player-b@e2e.test", "Blake", "Two");
			var gameId = await Seeder.CreateSeatedHoldEmGameAsync("E2E seated table", avery, blake);

			var averySession = await LoginPlayerAsync(avery);
			var blakeSession = await LoginPlayerAsync(blake);

			await averySession.OpenTableAsync(gameId);
			await blakeSession.OpenTableAsync(gameId);

			await averySession.WaitForHubConnectionAsync();
			await blakeSession.WaitForHubConnectionAsync();

			await averySession.ExpectSeatedAsync(avery.DisplayName);
			await averySession.ExpectSeatedAsync(blake.DisplayName);
			await blakeSession.ExpectSeatedAsync(avery.DisplayName);
			await blakeSession.ExpectSeatedAsync(blake.DisplayName);
		});
	}

	[E2EFact]
	public async Task AutoJoin_OtherPlayerSeesNewSeat_ViaSignalR()
	{
		await ExecuteAsync(async () =>
		{
			var avery = await Seeder.CreatePlayerAsync("player-a@e2e.test", "Avery", "One");
			var blake = await Seeder.CreatePlayerAsync("player-b@e2e.test", "Blake", "Two");
			var gameId = await Seeder.CreateOpenHoldEmGameAsync("E2E open table");

			var averySession = await LoginPlayerAsync(avery);
			await averySession.OpenTableAsync(gameId, autoJoin: true);
			await averySession.ConfirmBuyInAsync();
			await averySession.WaitForHubConnectionAsync();

			var blakeSession = await LoginPlayerAsync(blake);
			await blakeSession.OpenTableAsync(gameId, autoJoin: true);
			await blakeSession.ConfirmBuyInAsync();

			await averySession.ExpectSeatedAsync(blake.DisplayName, 30_000);
			await blakeSession.ExpectSeatedAsync(avery.DisplayName, 30_000);
		});
	}
}
