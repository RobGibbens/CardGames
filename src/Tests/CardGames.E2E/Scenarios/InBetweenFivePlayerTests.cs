using CardGames.E2E.Infrastructure;

namespace CardGames.E2E.Scenarios;

[Collection(PlaywrightE2ECollection.Name)]
public sealed class InBetweenFivePlayerTests : E2ETestBase
{
	private static readonly (string FirstName, string Email)[] Players =
	[
		("Avery", "player-a@e2e.test"),
		("Blake", "player-b@e2e.test"),
		("Casey", "player-c@e2e.test"),
		("Drew", "player-d@e2e.test"),
		("Ellis", "player-e@e2e.test")
	];

	public InBetweenFivePlayerTests(PlaywrightAppFixture fixture)
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

			var gameId = await Seeder.CreateSeatedInBetweenGameAsync(
				"E2E five-handed in-between",
				players,
				ante: InBetweenFiveMaxCatalog.Ante);
			var table = await InBetweenTable.OpenAsync(LoginPlayerAsync, Fixture.ApiBaseUrl, gameId, players);
			var ledger = new InBetweenLedger(Enumerable.Repeat(ScenarioSeeder.SeatedStack, players.Count).ToArray());
			var scripts = InBetweenFiveMaxCatalog.Hands;

			// InitializeAsync resets the database once. The hands below keep the same seats and browsers.
			for (var hand = 0; hand < scripts.Count; hand++)
			{
				var hasNext = hand < scripts.Count - 1;
				await table.PlayAsync(
					scripts[hand],
					ledger,
					startGame: hand == 0,
					nextScript: hasNext ? scripts[hand + 1] : null);
			}

			Assert.Equal(4, scripts.Count);
			Assert.Equal(5_000, ledger.StartingSum);
			Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());

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
