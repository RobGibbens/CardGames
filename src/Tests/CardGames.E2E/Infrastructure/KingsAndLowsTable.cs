using System.Globalization;
using Microsoft.Playwright;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Logged-in browsers at one Kings and Lows table. Plays a <see cref="KingsAndLowsHandScript" />
///     through drop or stay, the draw panel, and the deck overlay, and checks the dealer, pot, and stacks.
/// </summary>
public sealed class KingsAndLowsTable
{
	private readonly string _apiBaseUrl;
	private readonly Guid _gameId;
	private readonly IReadOnlyList<SeededPlayer> _players;
	private readonly int _seatCount;
	private readonly IReadOnlyList<PlayerSession> _sessions;

	private KingsAndLowsTable(
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

	public static async Task<KingsAndLowsTable> OpenAsync(
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

		return new KingsAndLowsTable(apiBaseUrl, gameId, players, sessions);
	}

	public async Task PlayAsync(
		KingsAndLowsHandScript script,
		int dealerSeat,
		KingsAndLowsLedger ledger,
		bool startGame,
		KingsAndLowsHandScript? nextScript = null,
		int nextDealerSeat = 0)
	{
		if (startGame)
		{
			await ScriptAsync(script, dealerSeat, roundRobinBySeat: true);
			await _sessions[0].StartGameAsync();
		}

		var armedNextHand = false;
		foreach (var action in script.Actions)
		{
			var session = Actor(dealerSeat, action.Actor);
			if (action.Kind is KingsAndLowsActionKind.Drop or KingsAndLowsActionKind.Stay)
			{
				await StepAsync(script, $"{action.Actor} {action.Kind}", async () =>
				{
					await session.WaitForDropOrStayAsync();
					if (!armedNextHand)
					{
						if (nextScript is not null)
							await ScriptAsync(nextScript, nextDealerSeat, roundRobinBySeat: false);

						await ExpectDealerAsync(dealerSeat);
						await ExpectHoleCardsAsync(script, dealerSeat);
						await ExpectPotAsync(script.OpeningPot);
						armedNextHand = true;
					}

					await session.DropOrStayAsync(action.Kind == KingsAndLowsActionKind.Stay);
				});
				continue;
			}

			if (action.Kind is KingsAndLowsActionKind.StandPat or KingsAndLowsActionKind.Discard)
			{
				await StepAsync(script, $"{action.Actor} {action.Kind}", async () =>
				{
					await ExpectHoleCardsAsync(dealerSeat, action.Actor, script.HoleCards[action.Actor]);
					if (action.Kind == KingsAndLowsActionKind.StandPat)
					{
						await session.StandPatAsync();
					}
					else
					{
						var discards = action.Discards
							?? throw new InvalidOperationException("A discard needs cards.");
						await session.DiscardCardsAsync(discards);
					}

					await ExpectHoleCardsAsync(
						dealerSeat,
						action.Actor,
						KingsAndLowsFiveMaxCatalog.FinalHand(script, action.Actor));
				});
				continue;
			}

			if (action.Kind == KingsAndLowsActionKind.DeckKeepAll)
			{
				await StepAsync(script, $"{action.Actor} keeps the deck", () => session.KeepDeckAsync());
				continue;
			}

			throw new InvalidOperationException($"Unexpected action {action.Kind}.");
		}

		if (script.Showdown == KingsAndLowsShowdownKind.Carried)
		{
			await StepAsync(script, "carried pot", async () =>
			{
				ledger.Apply(script, dealerSeat);
				await ExpectPotAsync(script.OpeningPot);
				await ExpectStacksAsync(ledger.Stacks);
				await ExpectDealerAsync(nextDealerSeat);
				await ExpectNoShowdownAsync();
				await ExpectNoPotMatchAsync();
			});
		}
		else
		{
			await StepAsync(script, "showdown", async () =>
			{
				await ExpectShowdownAsync(script, dealerSeat);
				ledger.Apply(script, dealerSeat);
				await ExpectStacksAsync(ledger.Stacks);
				await ExpectNoPotMatchAsync();
				await Task.WhenAll(_sessions.Select(session => session.CloseShowdownAsync()));
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

	private async Task ScriptAsync(KingsAndLowsHandScript script, int dealerSeat, bool roundRobinBySeat)
	{
		var holes = script.HoleCards.ToDictionary(
			pair => SeatIndex(dealerSeat, pair.Key),
			pair => pair.Value);
		var cards = KingsAndLowsDeckScript.Build(
			holes,
			KingsAndLowsFiveMaxCatalog.DrawStub(script),
			dealerSeat,
			roundRobinBySeat);
		await ScriptedDeckClient.SetAsync(_apiBaseUrl, _gameId, cards);
	}

	private int SeatIndex(int dealerSeat, HoldEmPosition position) =>
		HoldEmSeats.Index(_seatCount, dealerSeat, position);

	private PlayerSession Actor(int dealerSeat, HoldEmPosition position) =>
		_sessions[SeatIndex(dealerSeat, position)];

	private PlayerSession Host => _sessions[0];

	private async Task ExpectDealerAsync(int dealerSeat)
	{
		var page = Host.Page;
		await Assertions.Expect(page.Locator("[data-testid='dealer-button']")).ToHaveCountAsync(
			1,
			new LocatorAssertionsToHaveCountOptions { Timeout = 20_000 });
		await Assertions.Expect(Seat(page, dealerSeat).Locator("[data-testid='dealer-button']")).ToBeVisibleAsync(
			new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
	}

	private async Task ExpectHoleCardsAsync(KingsAndLowsHandScript script, int dealerSeat)
	{
		var checks = script.HoleCards.Select(pair =>
			ExpectHoleCardsAsync(dealerSeat, pair.Key, pair.Value));
		await Task.WhenAll(checks);
	}

	private Task ExpectHoleCardsAsync(
		int dealerSeat,
		HoldEmPosition actor,
		IReadOnlyList<string> cards) =>
		ExpectCardsAsync(
			Actor(dealerSeat, actor).Page.Locator("[data-testid='hole-card']"),
			cards,
			ordered: false);

	private async Task ExpectShowdownAsync(KingsAndLowsHandScript script, int dealerSeat)
	{
		var overlay = Host.Page.Locator("[aria-label='Showdown results']");
		await overlay.WaitForAsync(new LocatorWaitForOptions
		{
			State = WaitForSelectorState.Visible,
			Timeout = 45_000
		});

		if (script.Showdown == KingsAndLowsShowdownKind.DeckWon)
		{
			await Assertions.Expect(overlay.Locator(".showdown-title")).ToHaveTextAsync(
				"Showdown",
				new LocatorAssertionsToHaveTextOptions { Timeout = 15_000 });
			await Assertions.Expect(overlay.Locator(".showdown-subtitle")).ToContainTextAsync(
				"The deck wins this round",
				new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
			await Assertions.Expect(overlay.Locator("[data-testid='showdown-winner']")).ToHaveTextAsync("Deck");
			await Assertions.Expect(overlay.Locator("[data-testid='showdown-verb']")).ToHaveTextAsync(
				"wins this hand");
			await Assertions.Expect(overlay.Locator("[data-testid='showdown-amount']")).ToHaveCountAsync(0);
			return;
		}

		var winner = script.Winner ?? throw new InvalidOperationException($"'{script.Name}' has no winner.");
		var winnerSeat = SeatIndex(dealerSeat, winner);
		await Assertions.Expect(overlay.Locator("[data-testid='showdown-winner']")).ToHaveTextAsync(
			_players[winnerSeat].DisplayName,
			new LocatorAssertionsToHaveTextOptions { Timeout = 15_000 });
		await Assertions.Expect(overlay.Locator("[data-testid='showdown-verb']")).ToHaveTextAsync("wins the hand");
		await ExpectAmountAsync(overlay.Locator("[data-testid='showdown-amount']"), script.AwardedAmount);
	}

	private async Task ExpectNoShowdownAsync()
	{
		await Assertions.Expect(Host.Page.Locator("[aria-label='Showdown results']")).ToHaveCountAsync(0);
	}

	private async Task ExpectNoPotMatchAsync()
	{
		await Assertions.Expect(Host.Page.Locator(".pot-matching-overlay")).ToHaveCountAsync(0);
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

	private async Task StepAsync(KingsAndLowsHandScript script, string where, Func<Task> step)
	{
		try
		{
			await step();
		}
		catch (Exception exception)
		{
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
