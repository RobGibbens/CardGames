using CardGames.E2E.Infrastructure;

namespace CardGames.E2E.Scenarios;

[Collection(PlaywrightE2ECollection.Name)]
public sealed class HoldEmFivePlayerTests : E2ETestBase
{
	private static readonly (string FirstName, string Email)[] Players =
	[
		("Avery", "player-a@e2e.test"),
		("Blake", "player-b@e2e.test"),
		("Casey", "player-c@e2e.test"),
		("Drew", "player-d@e2e.test"),
		("Ellis", "player-e@e2e.test")
	];

	public HoldEmFivePlayerTests(PlaywrightAppFixture fixture)
		: base(fixture)
	{
	}

	[E2EFact(600_000)]
	public async Task FiveHandedOrbit_EachSeatDealsOnce_AndCashierMatchesTheTable()
	{
		await ExecuteAsync(async () =>
		{
			var players = new List<SeededPlayer>(Players.Length);
			foreach (var (firstName, email) in Players)
				players.Add(await Seeder.CreatePlayerAsync(email, firstName, "Table"));

			var gameId = await Seeder.CreateSeatedHoldEmGameAsync(
				"E2E five-handed hold'em",
				players,
				smallBlind: 5,
				bigBlind: 10);
			var table = await HoldEmTable.OpenAsync(LoginPlayerAsync, Fixture.ApiBaseUrl, gameId, players);
			var ledger = new HoldEmLedger(Enumerable.Repeat(ScenarioSeeder.SeatedStack, players.Count).ToArray());
			var scripts = HoldEmFiveMaxCatalog.Orbit;

			for (var hand = 0; hand < scripts.Count; hand++)
			{
				var dealerSeat = hand % players.Count;
				var hasNext = hand < scripts.Count - 1;
				await table.PlayAsync(
					scripts[hand],
					dealerSeat,
					ledger,
					startGame: hand == 0,
					nextScript: hasNext ? scripts[hand + 1] : null,
					nextDealerSeat: (hand + 1) % players.Count);
			}

			Assert.Equal(5_000, ledger.StartingSum);
			Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());

			for (var seat = 0; seat < players.Count; seat++)
			{
				var tableNet = ledger.Stacks[seat] - ScenarioSeeder.SeatedStack;
				Assert.Equal(
					ScenarioSeeder.StartingChips + tableNet,
					await Seeder.GetCashierBalanceAsync(players[seat].PlayerId));
			}

			await table.ExpectNoAppExceptionsAsync();
		});
	}
}
