using System.Globalization;
using Microsoft.Playwright;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Logged-in browsers at one In-Between table. Plays a <see cref="InBetweenHandScript" />
///     through the in-between overlay and checks the boundary cards, pot, and stacks.
/// </summary>
public sealed class InBetweenTable
{
	private readonly string _apiBaseUrl;
	private readonly Guid _gameId;
	private readonly IReadOnlyList<PlayerSession> _sessions;

	private InBetweenTable(
		string apiBaseUrl,
		Guid gameId,
		IReadOnlyList<PlayerSession> sessions)
	{
		_apiBaseUrl = apiBaseUrl;
		_gameId = gameId;
		_sessions = sessions;
	}

	public static async Task<InBetweenTable> OpenAsync(
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

		return new InBetweenTable(apiBaseUrl, gameId, sessions);
	}

	public async Task PlayAsync(
		InBetweenHandScript script,
		InBetweenLedger ledger,
		bool startGame,
		InBetweenHandScript? nextScript = null)
	{
		if (startGame)
		{
			await ScriptAsync(script);
			await _sessions[0].StartGameAsync();
		}

		var armedNextHand = false;
		var anteApplied = false;
		for (var index = 0; index < script.Actions.Count; index++)
		{
			var action = script.Actions[index];
			var isLast = index == script.Actions.Count - 1;
			await StepAsync(script, action, async () =>
			{
				if (isLast && nextScript is not null && !armedNextHand)
				{
					await ScriptAsync(nextScript);
					armedNextHand = true;
				}

				var session = _sessions[action.Seat];
				await WaitForTurnAsync(session, action);
				if (!anteApplied)
				{
					ledger.ApplyAnte(InBetweenFiveMaxCatalog.Ante, _sessions.Count);
					ledger.EnsureConserved(script.Name, InBetweenFiveMaxCatalog.OpeningPot);
					await ExpectPotAsync(InBetweenFiveMaxCatalog.OpeningPot);
					await ExpectDealerAsync(0);
					anteApplied = true;
				}

				await ExpectCommunityAsync(action.Boundaries);
				await SubmitAsync(session, action);

				if (action.ThirdCard is not null)
					await ExpectCommunityAsync(action.DealtCards);

				ledger.Apply(action);
				ledger.EnsureConserved(script.Name, action.PotAfter);
				await ExpectPotAsync(action.PotAfter);
				await ExpectStacksAsync(ledger.Stacks);
			});
		}

		await ExpectNoAppExceptionsAsync();
	}

	/// <summary>
	///     Fails when a seated browser recorded an uncaught error, a console error, or the Blazor error UI.
	/// </summary>
	public async Task ExpectNoAppExceptionsAsync()
	{
		var problems = await CollectAppExceptionsAsync();
		if (problems.Count > 0)
			Assert.Fail(string.Join(Environment.NewLine, problems));
	}

	private Task ScriptAsync(InBetweenHandScript script) =>
		ScriptedDeckClient.SetAsync(_apiBaseUrl, _gameId, InBetweenDeckScript.Build(script.DealtCards));

	private PlayerSession Host => _sessions[0];

	private async Task WaitForTurnAsync(PlayerSession session, InBetweenAction action)
	{
		var page = session.Page;
		if (action.ExpectAceChoice)
		{
			await page.Locator(".ib-ace-high-btn").WaitForAsync(new LocatorWaitForOptions
			{
				State = WaitForSelectorState.Visible,
				Timeout = 45_000
			});
			await Assertions.Expect(page.Locator(".ib-pass-btn")).ToHaveCountAsync(0);
			return;
		}

		await page.Locator(".ib-pass-btn").WaitForAsync(new LocatorWaitForOptions
		{
			State = WaitForSelectorState.Visible,
			Timeout = 45_000
		});
		await Assertions.Expect(page.Locator(".ib-ace-high-btn")).ToHaveCountAsync(0);
	}

	private async Task SubmitAsync(PlayerSession session, InBetweenAction action)
	{
		var page = session.Page;
		if (action.ExpectAceChoice)
		{
			var aceButton = action.AceIsHigh == true ? ".ib-ace-high-btn" : ".ib-ace-low-btn";
			await page.Locator(aceButton).ClickAsync();
			await page.Locator(".ib-pass-btn").WaitForAsync(new LocatorWaitForOptions
			{
				State = WaitForSelectorState.Visible,
				Timeout = 20_000
			});
		}

		switch (action.Kind)
		{
			case InBetweenActionKind.Pass:
				await page.Locator(".ib-pass-btn").ClickAsync();
				break;
			case InBetweenActionKind.Bet:
				await SetBetAsync(page, action.Amount ?? throw new InvalidOperationException("A bet needs an amount."));
				await page.Locator(".ib-bet-btn").ClickAsync();
				break;
			case InBetweenActionKind.BetPot:
				await page.Locator(".ib-pot-btn").ClickAsync();
				break;
			default:
				throw new InvalidOperationException($"Unexpected action {action.Kind}.");
		}
	}

	private static async Task SetBetAsync(IPage page, int amount)
	{
		var slider = page.Locator(".in-between-panel .bet-slider");
		await slider.WaitForAsync(new LocatorWaitForOptions
		{
			State = WaitForSelectorState.Visible,
			Timeout = 20_000
		});
		await slider.EvaluateAsync(
			"""
			(el, bet) => {
				el.value = String(bet);
				el.dispatchEvent(new Event('input', { bubbles: true }));
			}
			""",
			amount);
		await Assertions.Expect(page.Locator(".ib-bet-btn")).ToBeVisibleAsync(
			new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
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

	private Task ExpectCommunityAsync(IReadOnlyList<string> cards) =>
		ExpectCardsAsync(Host.Page.Locator("[data-testid='community-card']"), cards);

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

	private static ILocator Seat(IPage page, int seat) =>
		page.Locator($"[data-testid='seat'][data-seat-index='{seat.ToString(CultureInfo.InvariantCulture)}']");

	private static async Task ExpectCardsAsync(ILocator cards, IReadOnlyList<string> expected)
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);
		var last = "";
		while (true)
		{
			var actual = await ReadCardsAsync(cards);
			last = string.Join(" ", actual);
			if (actual.SequenceEqual(expected, StringComparer.Ordinal))
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

	private async Task StepAsync(InBetweenHandScript script, InBetweenAction action, Func<Task> step)
	{
		try
		{
			await step();
			await ExpectNoAppExceptionsAsync();
		}
		catch (Exception exception)
		{
			var where = $"{script.Name} seat {action.Seat} {action.Kind}";
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
			foreach (var error in session.PageErrors)
				problems.Add($"{session.Player.Email} page error: {error}");

			foreach (var error in session.ConsoleErrors)
				problems.Add($"{session.Player.Email} console error: {error}");

			if (await session.Page.Locator("#blazor-error-ui").IsVisibleAsync())
				problems.Add($"{session.Player.Email} shows the Blazor error UI.");
		}

		return problems;
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
