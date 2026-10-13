using CardGames.E2E.Infrastructure;

namespace CardGames.E2E.Scenarios;

[Collection(PlaywrightE2ECollection.Name)]
public sealed class ScrewYourNeighborFivePlayerTests : E2ETestBase
{
	private static readonly (string FirstName, string Email)[] Players =
	[
		("Avery", "player-a@e2e.test"),
		("Blake", "player-b@e2e.test"),
		("Casey", "player-c@e2e.test"),
		("Drew", "player-d@e2e.test"),
		("Ellis", "player-e@e2e.test")
	];

	public ScrewYourNeighborFivePlayerTests(PlaywrightAppFixture fixture)
		: base(fixture)
	{
	}

	[E2EFact(600_000)]
	public async Task FourHands_KeepFivePlayersSeated_AndNoBrowserExceptions()
	{
		await ExecuteAsync(async () =>
		{
			var players = new List<SeededPlayer>(Players.Length);
			foreach (var (firstName, email) in Players)
				players.Add(await Seeder.CreatePlayerAsync(email, firstName, "Table"));

			var gameId = await Seeder.CreateSeatedScrewYourNeighborGameAsync(
				"E2E five-handed screw your neighbor",
				players,
				stackSize: ScrewYourNeighborFiveMaxCatalog.StackSize);
			var table = await ScrewYourNeighborTable.OpenAsync(LoginPlayerAsync, Fixture.ApiBaseUrl, gameId, players);

			// InitializeAsync resets the database once. The hands below keep the same seats, browsers, and deck.
			await table.PlayAsync(ScrewYourNeighborFiveMaxCatalog.Hands, ScrewYourNeighborFiveMaxCatalog.NextCard);

			Assert.Equal(4, ScrewYourNeighborFiveMaxCatalog.Hands.Count);
			Assert.Equal(ScrewYourNeighborFiveMaxCatalog.TableChips, 1_500);

			for (var seat = 0; seat < players.Count; seat++)
			{
				Assert.Equal(
					ScenarioSeeder.StartingChips,
					await Seeder.GetCashierBalanceAsync(players[seat].PlayerId));
			}

			await table.ExpectNoAppExceptionsAsync();
		});
	}
}
