using System.Globalization;
using Microsoft.Playwright;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Logged-in browsers at one Good, Bad, and Ugly table. Plays a
///     <see cref="GoodBadUglyHandScript" /> through the action panel and fails if the app
///     surfaces an exception.
/// </summary>
public sealed class GoodBadUglyTable
{
	private readonly string _apiBaseUrl;
	private readonly Guid _gameId;
	private readonly IReadOnlyList<SeededPlayer> _players;
	private readonly int _seatCount;
	private readonly IReadOnlyList<PlayerSession> _sessions;

	private GoodBadUglyTable(
		string apiBaseUrl,
		Guid gameId,
		IReadOnlyList<SeededPlayer> players,
		IReadOnlyList<PlayerSession> sessions)
	{
		_apiBaseUrl = apiBaseUrl;
		_gameId = gameId;
		_players = players;
		_seatCount = players.Count;
		_sessions = sessions;
	}

	public static async Task<GoodBadUglyTable> OpenAsync(
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

		return new GoodBadUglyTable(apiBaseUrl, gameId, players, sessions);
	}

	public async Task PlayAsync(
		GoodBadUglyHandScript script,
		int dealerSeat,
		GoodBadUglyLedger ledger,
		bool startGame,
		GoodBadUglyHandScript? nextScript = null,
		int nextDealerSeat = 0)
	{
		if (startGame)
		{
			await ScriptAsync(script, dealerSeat, communityFirst: true);
			await _sessions[0].StartGameAsync();
		}

		var armed = false;
		foreach (var action in script.Actions)
		{
			var session = Actor(dealerSeat, action.Actor);
			await StepAsync(script, action, async () =>
			{
				await session.WaitForActionPanelAsync();
				if (!armed)
				{
					if (nextScript is not null)
						await ScriptAsync(nextScript, nextDealerSeat, communityFirst: true);

					await ExpectDealerAsync(dealerSeat);
					await ExpectNoBlindsAsync();
					await ExpectCommunityLabelsAsync();
					await ExpectPotAsync(GoodBadUglyFiveMaxCatalog.OpeningPot);
					await ExpectHoleCardsAsync(script.HoleCards, dealerSeat);
					await ExpectCommunityAsync([]);
					armed = true;
				}

				if (action.CommunityCount is int communityCount)
					await ExpectCommunityAsync(script.Community.Take(communityCount).ToArray());

				if (action.HoleCards is not null)
					await ExpectHoleCardsAsync(action.HoleCards, dealerSeat);

				await SubmitAsync(session, action);

				if (action.PotAfter is int pot)
					await ExpectPotAsync(pot);

				if (action.StreetBet is int streetBet)
					await ExpectStreetBetAsync(SeatIndex(dealerSeat, action.Actor), streetBet);
			});
		}

		await StepAsync(script, null, async () =>
		{
			await ExpectShowdownAsync(script, dealerSeat);
			ledger.Apply(script, dealerSeat);
			await ExpectStacksAsync(ledger.Stacks);
			await Task.WhenAll(_sessions.Select(session => session.CloseShowdownAsync()));
		});

		await ExpectNoAppExceptionsAsync();
	}

	public async Task ExpectNoAppExceptionsAsync()
	{
		var problems = await CollectAppExceptionsAsync();
		if (problems.Count > 0)
			Assert.Fail(string.Join(Environment.NewLine, problems));
	}

	private async Task ScriptAsync(GoodBadUglyHandScript script, int dealerSeat, bool communityFirst)
	{
		var cards = GoodBadUglyDeckScript.Build(dealerSeat, script, communityFirst);
		await ScriptedDeckClient.SetAsync(_apiBaseUrl, _gameId, cards);
	}

	private int SeatIndex(int dealerSeat, HoldEmPosition position) =>
		HoldEmSeats.Index(_seatCount, dealerSeat, position);

	private PlayerSession Actor(int dealerSeat, HoldEmPosition position) =>
		_sessions[SeatIndex(dealerSeat, position)];

	private PlayerSession Host => _sessions[0];

	private async Task SubmitAsync(PlayerSession session, GoodBadUglyAction action)
	{
		switch (action.Kind)
		{
			case GoodBadUglyActionKind.Fold:
				await session.FoldAsync();
				break;
			case GoodBadUglyActionKind.Check:
				await session.CheckAsync();
				break;
			case GoodBadUglyActionKind.Call:
				await session.CallAsync(action.CallAmount);
				break;
			case GoodBadUglyActionKind.Bet:
				await session.BetAsync(action.Amount ?? throw new InvalidOperationException("A bet needs an amount."));
				break;
			case GoodBadUglyActionKind.RaiseTo:
				await session.RaiseToAsync(action.Amount ?? throw new InvalidOperationException("A raise needs an amount."));
				break;
			default:
				throw new InvalidOperationException($"Unexpected action {action.Kind}.");
		}
	}

	private async Task ExpectDealerAsync(int dealerSeat)
	{
		var page = Host.Page;
		await Assertions.Expect(page.Locator("[data-testid='dealer-button']")).ToHaveCountAsync(
			1,
			new LocatorAssertionsToHaveCountOptions { Timeout = 20_000 });
		await Assertions.Expect(Seat(page, dealerSeat).Locator("[data-testid='dealer-button']")).ToBeVisibleAsync(
			new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
	}

	private async Task ExpectNoBlindsAsync()
	{
		var page = Host.Page;
		await Assertions.Expect(page.Locator("[data-testid='small-blind']")).ToHaveCountAsync(0);
		await Assertions.Expect(page.Locator("[data-testid='big-blind']")).ToHaveCountAsync(0);
	}

	private async Task ExpectCommunityLabelsAsync()
	{
		await Assertions.Expect(Host.Page.Locator(".community-card-label")).ToHaveTextAsync(
			["The Good", "The Bad", "The Ugly"],
			new LocatorAssertionsToHaveTextOptions { Timeout = 20_000 });
	}

	private async Task ExpectHoleCardsAsync(
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> holeCards,
		int dealerSeat)
	{
		var checks = holeCards.Select(pair =>
			ExpectCardsAsync(
				Seat(Actor(dealerSeat, pair.Key).Page, SeatIndex(dealerSeat, pair.Key))
					.Locator("[data-testid='hole-card']"),
				pair.Value,
				ordered: false));
		await Task.WhenAll(checks);
	}

	private Task ExpectCommunityAsync(IReadOnlyList<string> cards) =>
		ExpectCardsAsync(Host.Page.Locator("[data-testid='community-card']"), cards, ordered: true);

	private async Task ExpectShowdownAsync(GoodBadUglyHandScript script, int dealerSeat)
	{
		var winnerSeat = SeatIndex(dealerSeat, script.Winner);
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
		else
		{
			await Assertions.Expect(overlay.Locator(".winner-hand-stage")).ToHaveCountAsync(1);
		}

		if (!string.IsNullOrWhiteSpace(script.WinnerDescription))
		{
			await Assertions.Expect(overlay.Locator(".winner-hand-callout")).ToHaveTextAsync(
				script.WinnerDescription,
				new LocatorAssertionsToHaveTextOptions { Timeout = 15_000 });
		}

		foreach (var position in script.Eliminated.Where(position => position != script.Winner))
		{
			var name = _players[SeatIndex(dealerSeat, position)].DisplayName;
			var row = overlay.Locator(".other-hand-row").Filter(new LocatorFilterOptions { HasText = name });
			await Assertions.Expect(row).ToContainTextAsync(
				"Dead Hand (The Ugly)",
				new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
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

	private async Task StepAsync(GoodBadUglyHandScript script, GoodBadUglyAction? action, Func<Task> step)
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
			var problems = await CollectAppExceptionsAsync();
			var app = problems.Count == 0
				? ""
				: $" App exceptions: {string.Join(" | ", problems)}";
			throw new InvalidOperationException($"Hand '{script.Name}' failed during {where}.{app}", exception);
		}
	}

	private async Task<List<string>> CollectAppExceptionsAsync()
	{
		var problems = new List<string>();
		foreach (var session in _sessions)
		{
			foreach (var error in session.PageErrors.Where(error => !IsStartupNoise(error)))
				problems.Add($"{session.Player.Email} page error: {error}");

			foreach (var error in session.ConsoleErrors.Where(error => !IsStartupNoise(error)))
				problems.Add($"{session.Player.Email} console error: {error}");

			if (await session.Page.Locator("#blazor-error-ui").IsVisibleAsync())
				problems.Add($"{session.Player.Email} shows the Blazor error UI.");
		}

		return problems;
	}

	/// <summary>
	///     Login and the first document load emit these in headless Chromium before a hand starts.
	///     They are the same on every seat and are not reported by the Blazor error UI.
	/// </summary>
	private static bool IsStartupNoise(string error) =>
		error.Contains("Cannot use import statement outside a module", StringComparison.Ordinal) ||
		error.Contains("Failed to load resource: the server responded with a status of 404", StringComparison.Ordinal) ||
		error.Contains("Resident credentials or empty 'allowCredentials'", StringComparison.Ordinal);

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
