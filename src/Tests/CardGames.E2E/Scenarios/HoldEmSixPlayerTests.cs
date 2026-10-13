using CardGames.E2E.Infrastructure;

namespace CardGames.E2E.Scenarios;

[Collection(PlaywrightE2ECollection.Name)]
public sealed class HoldEmSixPlayerTests : E2ETestBase
{
	private static readonly (string FirstName, string Email)[] Players =
	[
		("Avery", "player-a@e2e.test"),
		("Blake", "player-b@e2e.test"),
		("Casey", "player-c@e2e.test"),
		("Drew", "player-d@e2e.test"),
		("Ellis", "player-e@e2e.test"),
		("Finley", "player-f@e2e.test")
	];

	public HoldEmSixPlayerTests(PlaywrightAppFixture fixture)
		: base(fixture)
	{
	}

	[E2EFact(240_000)]
	public async Task FoldToBigBlind_AwardsPostedBlinds()
	{
		await ExecuteAsync(() => PlayHandAsync(HoldEmHandCatalog.FoldToBigBlind));
	}

	[E2EFact(240_000)]
	public async Task OpenRaise_FlopBet_TakesThePot()
	{
		await ExecuteAsync(() => PlayHandAsync(HoldEmHandCatalog.OpenRaiseAndFlopBet));
	}

	[E2EFact(240_000)]
	public async Task CheckDown_AwardsBestHandAndRevealsBoardByStreet()
	{
		await ExecuteAsync(() => PlayHandAsync(HoldEmHandCatalog.Checkdown));
	}

	[E2EFact(240_000)]
	public async Task RiverRaise_CallClosesTheAction()
	{
		await ExecuteAsync(() => PlayHandAsync(HoldEmHandCatalog.RiverRaise));
	}

	[E2EFact(240_000)]
	public async Task SplitBoard_OddChipGoesToEarliestSeatAfterButton()
	{
		await ExecuteAsync(() => PlayHandAsync(HoldEmHandCatalog.SplitPot));
	}

	[E2EFact(240_000)]
	public async Task ShortAllIn_MainPotAndSidePotAwardDifferentWinners()
	{
		await ExecuteAsync(() => PlayHandAsync(
			HoldEmHandCatalog.SidePot,
			stacks: [1_000, 1_000, 200, 80, 1_000, 1_000]));
	}

	[E2EFact(240_000)]
	public async Task ThreeTiers_DifferentPlayersWinEachPot()
	{
		await ExecuteAsync(() => PlayHandAsync(
			HoldEmHandCatalog.ThreeTiers,
			stacks: [1_000, 40, 1_000, 80, 1_000, 1_000]));
	}

	[E2EFact(240_000)]
	public async Task FoldedChipsRemain_RaiserTakesTheBlinds()
	{
		await ExecuteAsync(() => PlayHandAsync(HoldEmHandCatalog.FoldedChipsRemain));
	}

	[E2EFact(240_000)]
	public async Task SplitMain_SidePotGoesToTheBigBlind()
	{
		await ExecuteAsync(() => PlayHandAsync(
			HoldEmHandCatalog.SplitMainDifferentSideWinner,
			stacks: [1_000, 50, 1_000, 50, 1_000, 1_000]));
	}

	[E2EFact(240_000)]
	public async Task UncalledShove_RefundsTheExcessAndAwardsTheMatchedPot()
	{
		await ExecuteAsync(() => PlayHandAsync(
			HoldEmHandCatalog.UncalledRefund,
			stacks: [1_000, 1_000, 100, 1_000, 1_000, 1_000]));
	}

	[E2EFact(900_000)]
	public async Task ThreeOrbits_EachPlayerPostsBothBlindsAndDealsThreeTimes()
	{
		await ExecuteAsync(async () =>
		{
			var (players, table) = await OpenTableAsync();
			var ledger = new HoldEmLedger(Enumerable.Repeat(ScenarioSeeder.SeatedStack, HoldEmSeats.Count).ToArray());
			var scripts = HoldEmHandCatalog.Orbit;

			for (var hand = 0; hand < HoldEmSeats.Count * 3; hand++)
			{
				var dealerSeat = hand % HoldEmSeats.Count;
				var script = scripts[hand % scripts.Count];
				var hasNext = hand < HoldEmSeats.Count * 3 - 1;
				await table.PlayAsync(
					script,
					dealerSeat,
					ledger,
					startGame: hand == 0,
					nextScript: hasNext ? scripts[(hand + 1) % scripts.Count] : null,
					nextDealerSeat: (hand + 1) % HoldEmSeats.Count);
			}

			Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());
			// Buy-in stays on the table. Each hand's net is settled to the cashier.
			var tableNet = ledger.Stacks[0] - ScenarioSeeder.SeatedStack;
			Assert.Equal(
				ScenarioSeeder.StartingChips + tableNet,
				await Seeder.GetCashierBalanceAsync(players[0].PlayerId));
		});
	}

	private async Task PlayHandAsync(HoldEmHandScript script, IReadOnlyList<int>? stacks = null)
	{
		var (_, table) = await OpenTableAsync(stacks);
		var starting = stacks ?? Enumerable.Repeat(ScenarioSeeder.SeatedStack, HoldEmSeats.Count).ToArray();
		await table.PlayAsync(script, dealerSeat: 0, new HoldEmLedger(starting), startGame: true);
	}

	private async Task<(IReadOnlyList<SeededPlayer> Players, HoldEmTable Table)> OpenTableAsync(
		IReadOnlyList<int>? stacks = null)
	{
		var players = new List<SeededPlayer>(Players.Length);
		foreach (var (firstName, email) in Players)
			players.Add(await Seeder.CreatePlayerAsync(email, firstName, "Table"));

		var gameId = await Seeder.CreateSeatedHoldEmGameAsync(
			"E2E six-max hold'em",
			players,
			smallBlind: 5,
			bigBlind: 10,
			stacks);
		var table = await HoldEmTable.OpenAsync(LoginPlayerAsync, Fixture.ApiBaseUrl, gameId, players);
		return (players, table);
	}
}
