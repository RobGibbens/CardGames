using CardGames.E2E.Infrastructure;

namespace CardGames.E2E;

public class PhilsMomFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEachButton()
	{
		foreach (var script in PhilsMomFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(PhilsMomFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.Equal(5, script.Board.Count);
			Assert.All(script.HoleCards.Values, cards => Assert.Equal(4, cards.Count));

			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var card in script.HoleCards.Values.SelectMany(cards => cards).Concat(script.Board))
				Assert.True(used.Add(card), $"Card '{card}' is repeated in '{script.Name}'.");

			var discarded = new Dictionary<HoldEmPosition, List<string>>();
			var rounds = DiscardRounds(script);
			Assert.InRange(rounds.Count, 0, 2);
			for (var round = 0; round < rounds.Count; round++)
			{
				var expectedCommunity = round == 0 ? 0 : 3;
				Assert.All(rounds[round], action => Assert.Equal(expectedCommunity, action.CommunityCount));
				foreach (var action in rounds[round])
				{
					var card = action.Card ?? throw new InvalidOperationException("A discard needs one card.");
					Assert.Contains(card, script.HoleCards[action.Actor]);
					if (!discarded.TryGetValue(action.Actor, out var prior))
					{
						prior = [];
						discarded[action.Actor] = prior;
					}

					Assert.DoesNotContain(card, prior);
					prior.Add(card);
				}
			}

			Assert.All(discarded.Values, cards => Assert.InRange(cards.Count, 1, 2));

			for (var dealer = 0; dealer < PhilsMomFiveMaxCatalog.SeatCount; dealer++)
			{
				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(PhilsMomFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = PhilsMomDeckScript.Build(dealer, holes, script.Board);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
			}
		}
	}

	[Fact]
	public void Orbit_DiscardsInServerOrder_FromEachHandsButton()
	{
		for (var hand = 0; hand < PhilsMomFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = PhilsMomFiveMaxCatalog.Orbit[hand];
			var rounds = DiscardRounds(script);
			for (var round = 1; round <= 2; round++)
			{
				var actual = round <= rounds.Count
					? rounds[round - 1].Select(action => action.Actor).ToArray()
					: [];
				Assert.Equal(PhilsMomFiveMaxCatalog.DiscardOrder(script, dealerSeat: hand, round), actual);
			}
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, PhilsMomFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < PhilsMomFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = PhilsMomFiveMaxCatalog.Orbit[hand];
			ledger.Apply(script.Name, script.Nets, dealerSeat: hand);
		}

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(4, PhilsMomFiveMaxCatalog.Orbit.Count);
	}

	private static List<List<PhilsMomAction>> DiscardRounds(PhilsMomHandScript script)
	{
		var rounds = new List<List<PhilsMomAction>>();
		List<PhilsMomAction>? current = null;
		foreach (var action in script.Actions)
		{
			if (action.Kind != PhilsMomActionKind.Discard)
			{
				current = null;
				continue;
			}

			if (current is null)
			{
				current = [];
				rounds.Add(current);
			}

			current.Add(action);
		}

		return rounds;
	}
}
