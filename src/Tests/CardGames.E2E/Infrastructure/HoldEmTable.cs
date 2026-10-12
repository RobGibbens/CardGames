using System.Globalization;
using Microsoft.Playwright;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Six logged-in browsers at one Hold'em table. Plays a <see cref="HoldEmHandScript" />
///     through the action panel and checks the board, pot, blinds, and stacks.
/// </summary>
public sealed class HoldEmTable
{
	private readonly string _apiBaseUrl;
	private readonly Guid _gameId;
	private readonly IReadOnlyList<SeededPlayer> _players;
	private readonly IReadOnlyList<PlayerSession> _sessions;

	private HoldEmTable(
		string apiBaseUrl,
		Guid gameId,
		IReadOnlyList<SeededPlayer> players,
		IReadOnlyList<PlayerSession> sessions)
	{
		_apiBaseUrl = apiBaseUrl;
		_gameId = gameId;
		_players = players;
		_sessions = sessions;
	}

	public static async Task<HoldEmTable> OpenAsync(
		Func<SeededPlayer, Task<PlayerSession>> login,
		string apiBaseUrl,
		Guid gameId,
		IReadOnlyList<SeededPlayer> players)
	{
		var sessions = new List<PlayerSession>(players.Count);
		foreach (var player in players)
		{
			var session = await login(player);
			sessions.Add(session);
			await session.OpenTableAsync(gameId);
			await session.WaitForHubConnectionAsync();
		}

		return new HoldEmTable(apiBaseUrl, gameId, players, sessions);
	}

	public async Task PlayAsync(
		HoldEmHandScript script,
		int dealerSeat,
		HoldEmLedger ledger,
		bool startGame,
		HoldEmHandScript? nextScript = null,
		int nextDealerSeat = 0)
	{
		if (startGame)
		{
			await ScriptAsync(script, dealerSeat);
			await _sessions[0].StartGameAsync();
		}

		var armedNextHand = false;
		for (var index = 0; index < script.Actions.Count; index++)
		{
			var action = script.Actions[index];
			var session = Actor(dealerSeat, action.Actor);
			await StepAsync(script, action, async () =>
			{
				await session.WaitForActionPanelAsync();
				if (!armedNextHand)
				{
					if (nextScript is not null)
						await ScriptAsync(nextScript, nextDealerSeat);

					await ExpectButtonAndBlindsAsync(dealerSeat);
					await ExpectHoleCardsAsync(script, dealerSeat);
					await ExpectCommunityAsync([]);
					await ExpectPotAsync(15);
					armedNextHand = true;
				}

				if (action.CommunityCount is int communityCount)
					await ExpectCommunityAsync(script.Board.Take(communityCount).ToArray());

				await SubmitAsync(session, action);

				if (action.PotAfter is int pot)
					await ExpectPotAsync(pot);

				if (action.StreetBet is int streetBet)
					await ExpectStreetBetAsync(HoldEmSeats.Index(dealerSeat, action.Actor), streetBet);
			});
		}

		await StepAsync(script, null, async () =>
		{
			await ExpectShowdownAsync(script, dealerSeat);
			ledger.Apply(script, dealerSeat);
			await ExpectStacksAsync(ledger.Stacks);
			await Task.WhenAll(_sessions.Select(session => session.CloseShowdownAsync()));
		});
	}

	private async Task ScriptAsync(HoldEmHandScript script, int dealerSeat)
	{
		var holes = script.HoleCards.ToDictionary(
			pair => HoldEmSeats.Index(dealerSeat, pair.Key),
			pair => pair.Value);
		await ScriptedDeckClient.ScriptHoldEmAsync(_apiBaseUrl, _gameId, dealerSeat, holes, script.Board);
	}

	private PlayerSession Actor(int dealerSeat, HoldEmPosition position) =>
		_sessions[HoldEmSeats.Index(dealerSeat, position)];

	private PlayerSession Host => _sessions[0];

	private async Task SubmitAsync(PlayerSession session, HoldEmAction action)
	{
		switch (action.Kind)
		{
			case HoldEmActionKind.Fold:
				await session.FoldAsync();
				break;
			case HoldEmActionKind.Check:
				await session.CheckAsync();
				break;
			case HoldEmActionKind.Call:
				await session.CallAsync(action.CallAmount);
				break;
			case HoldEmActionKind.Bet:
				await session.BetAsync(action.Amount ?? throw new InvalidOperationException("A bet needs an amount."));
				break;
			case HoldEmActionKind.RaiseTo:
				await session.RaiseToAsync(action.Amount ?? throw new InvalidOperationException("A raise needs an amount."));
				break;
			case HoldEmActionKind.AllIn:
				await session.AllInAsync();
				break;
			default:
				throw new InvalidOperationException($"Unexpected action {action.Kind}.");
		}
	}

	private async Task ExpectButtonAndBlindsAsync(int dealerSeat)
	{
		var page = Host.Page;
		var smallBlind = (dealerSeat + (int)HoldEmPosition.SmallBlind) % HoldEmSeats.Count;
		var bigBlind = (dealerSeat + (int)HoldEmPosition.BigBlind) % HoldEmSeats.Count;

		await Assertions.Expect(page.Locator("[data-testid='dealer-button']")).ToHaveCountAsync(
			1,
			new LocatorAssertionsToHaveCountOptions { Timeout = 20_000 });
		await Assertions.Expect(Seat(page, dealerSeat).Locator("[data-testid='dealer-button']")).ToBeVisibleAsync(
			new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
		await Assertions.Expect(Seat(page, smallBlind).Locator("[data-testid='small-blind']")).ToBeVisibleAsync(
			new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
		await Assertions.Expect(Seat(page, bigBlind).Locator("[data-testid='big-blind']")).ToBeVisibleAsync(
			new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
	}

	private async Task ExpectHoleCardsAsync(HoldEmHandScript script, int dealerSeat)
	{
		var checks = script.HoleCards.Select(pair =>
			ExpectCardsAsync(
				Actor(dealerSeat, pair.Key).Page.Locator("[data-testid='hole-card']"),
				pair.Value,
				ordered: false));
		await Task.WhenAll(checks);
	}

	private Task ExpectCommunityAsync(IReadOnlyList<string> cards) =>
		ExpectCardsAsync(Host.Page.Locator("[data-testid='community-card']"), cards, ordered: true);

	private async Task ExpectShowdownAsync(HoldEmHandScript script, int dealerSeat)
	{
		var winnerSeat = HoldEmSeats.Index(dealerSeat, script.Winner);
		var overlay = Host.Page.Locator("[aria-label='Showdown results']");
		await overlay.WaitForAsync(new LocatorWaitForOptions
		{
			State = WaitForSelectorState.Visible,
			Timeout = 45_000
		});

		await Assertions.Expect(overlay.Locator("[data-testid='showdown-winner']")).ToHaveTextAsync(
			_players[winnerSeat].DisplayName,
			new LocatorAssertionsToHaveTextOptions { Timeout = 15_000 });
		await ExpectAmountAsync(overlay.Locator("[data-testid='showdown-amount']"), script.AwardedAmount);

		if (script.WonByFold)
		{
			await Assertions.Expect(overlay.Locator("[data-testid='showdown-verb']")).ToHaveTextAsync("wins by fold");
			await Assertions.Expect(overlay.Locator(".winner-hand-stage")).ToHaveCountAsync(0);
		}
	}

	private async Task ExpectStacksAsync(IReadOnlyList<int> stacks)
	{
		for (var seat = 0; seat < stacks.Count; seat++)
		{
			await ExpectAmountAsync(
				Seat(Host.Page, seat).Locator("[data-testid='seat-chips']"),
				stacks[seat]);
		}
	}

	private Task ExpectPotAsync(int amount) =>
		ExpectAmountAsync(Host.Page.Locator("[data-testid='pot-amount']"), amount);

	private Task ExpectStreetBetAsync(int seat, int amount) =>
		ExpectAmountAsync(Seat(Host.Page, seat).Locator("[data-testid='seat-bet']"), amount);

	private static ILocator Seat(IPage page, int seat) =>
		page.Locator($"[data-testid='seat'][data-seat-index='{seat.ToString(CultureInfo.InvariantCulture)}']");

	private static async Task ExpectCardsAsync(ILocator cards, IReadOnlyList<string> expected, bool ordered)
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);
		string last = "";
		while (true)
		{
			var actual = await ReadCardsAsync(cards);
			last = string.Join(" ", actual);
			var matches = ordered
				? actual.SequenceEqual(expected, StringComparer.Ordinal)
				: actual.OrderBy(card => card, StringComparer.Ordinal)
					.SequenceEqual(expected.OrderBy(card => card, StringComparer.Ordinal), StringComparer.Ordinal);
			if (matches)
				return;

			if (DateTime.UtcNow >= deadline)
				Assert.Fail($"Expected cards [{string.Join(" ", expected)}] but found [{last}].");

			await Task.Delay(150);
		}
	}

	private static async Task<List<string>> ReadCardsAsync(ILocator cards)
	{
		var count = await cards.CountAsync();
		var actual = new List<string>(count);
		for (var index = 0; index < count; index++)
		{
			var card = cards.Nth(index);
			var rank = await card.GetAttributeAsync("data-rank");
			var suit = await card.GetAttributeAsync("data-suit");
			if (string.IsNullOrWhiteSpace(rank) || string.IsNullOrWhiteSpace(suit))
				continue;

			actual.Add(ToCode(rank, suit));
		}

		return actual;
	}

	private static async Task ExpectAmountAsync(ILocator locator, int expected)
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);
		var last = "";
		while (true)
		{
			try
			{
				last = (await locator.InnerTextAsync(new LocatorInnerTextOptions { Timeout = 1_000 })).Trim();
				if (TryParseChips(last, out var amount) && amount == expected)
					return;
			}
			catch (Exception exception)
			{
				last = exception.Message;
			}

			if (DateTime.UtcNow >= deadline)
				Assert.Fail($"Expected {expected.ToString(CultureInfo.InvariantCulture)} but last saw '{last}'.");

			await Task.Delay(150);
		}
	}

	private static async Task StepAsync(HoldEmHandScript script, HoldEmAction? action, Func<Task> step)
	{
		try
		{
			await step();
		}
		catch (Exception exception)
		{
			var where = action is null
				? "showdown"
				: $"{action.Actor} {action.Kind}";
			throw new InvalidOperationException($"Hand '{script.Name}' failed during {where}.", exception);
		}
	}

	private static string ToCode(string rank, string suit)
	{
		var normalizedRank = rank.ToUpperInvariant() switch
		{
			"10" => "T",
			_ => rank.ToUpperInvariant()
		};
		var normalizedSuit = suit switch
		{
			"Hearts" => "h",
			"Diamonds" => "d",
			"Spades" => "s",
			"Clubs" => "c",
			_ => throw new InvalidOperationException($"Unexpected suit '{suit}'.")
		};
		return normalizedRank + normalizedSuit;
	}

	private static bool TryParseChips(string text, out int amount)
	{
		var digits = new string(text.Where(char.IsDigit).ToArray());
		return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out amount);
	}
}
