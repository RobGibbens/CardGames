using System.Globalization;
using Microsoft.Playwright;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Logged-in browsers at one Five Card Draw table. Plays a <see cref="FiveCardDrawHandScript" />
///     through the action panel and the draw panel, and checks the dealer, pot, and stacks.
/// </summary>
public sealed class FiveCardDrawTable
{
	private readonly string _apiBaseUrl;
	private readonly Guid _gameId;
	private readonly IReadOnlyList<SeededPlayer> _players;
	private readonly int _seatCount;
	private readonly IReadOnlyList<PlayerSession> _sessions;

	private FiveCardDrawTable(
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

	public static async Task<FiveCardDrawTable> OpenAsync(
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

		return new FiveCardDrawTable(apiBaseUrl, gameId, players, sessions);
	}

	public async Task PlayAsync(
		FiveCardDrawHandScript script,
		int dealerSeat,
		HoldEmLedger ledger,
		bool startGame,
		FiveCardDrawHandScript? nextScript = null,
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
			if (action.Kind is FiveCardDrawActionKind.StandPat or FiveCardDrawActionKind.Discard)
			{
				await StepAsync(script, $"{action.Actor} {action.Kind}", async () =>
				{
					await ExpectHoleCardsAsync(dealerSeat, action.Actor, script.HoleCards[action.Actor]);
					if (action.Kind == FiveCardDrawActionKind.StandPat)
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
						FiveCardDrawFiveMaxCatalog.FinalHand(script, action.Actor));
				});
				continue;
			}

			await StepAsync(script, $"{action.Actor} {action.Kind}", async () =>
			{
				await session.WaitForActionPanelAsync();
				if (!armedNextHand)
				{
					if (nextScript is not null)
						await ScriptAsync(nextScript, nextDealerSeat);

					await ExpectDealerAsync(dealerSeat);
					await ExpectHoleCardsAsync(script, dealerSeat);
					await ExpectPotAsync(FiveCardDrawFiveMaxCatalog.OpeningPot);
					armedNextHand = true;
				}

				await SubmitAsync(session, action);

				if (action.PotAfter is int pot)
					await ExpectPotAsync(pot);

				if (action.StreetBet is int streetBet)
					await ExpectStreetBetAsync(SeatIndex(dealerSeat, action.Actor), streetBet);
			});
		}

		await StepAsync(script, "showdown", async () =>
		{
			await ExpectShowdownAsync(script, dealerSeat);
			ledger.Apply(script.Name, script.Nets, dealerSeat);
			await ExpectStacksAsync(ledger.Stacks);
			await Task.WhenAll(_sessions.Select(session => session.CloseShowdownAsync()));
		});

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

	private async Task ScriptAsync(FiveCardDrawHandScript script, int dealerSeat)
	{
		var holes = script.HoleCards.ToDictionary(
			pair => SeatIndex(dealerSeat, pair.Key),
			pair => pair.Value);
		var cards = FiveCardDrawDeckScript.Build(
			dealerSeat,
			holes,
			FiveCardDrawFiveMaxCatalog.DrawStub(script));
		await ScriptedDeckClient.SetAsync(_apiBaseUrl, _gameId, cards);
	}

	private int SeatIndex(int dealerSeat, HoldEmPosition position) =>
		HoldEmSeats.Index(_seatCount, dealerSeat, position);

	private PlayerSession Actor(int dealerSeat, HoldEmPosition position) =>
		_sessions[SeatIndex(dealerSeat, position)];

	private PlayerSession Host => _sessions[0];

	private async Task SubmitAsync(PlayerSession session, FiveCardDrawAction action)
	{
		switch (action.Kind)
		{
			case FiveCardDrawActionKind.Fold:
				await session.FoldAsync();
				break;
			case FiveCardDrawActionKind.Check:
				await session.CheckAsync();
				break;
			case FiveCardDrawActionKind.Call:
				await session.CallAsync(action.CallAmount);
				break;
			case FiveCardDrawActionKind.Bet:
				await session.BetAsync(action.Amount ?? throw new InvalidOperationException("A bet needs an amount."));
				break;
			case FiveCardDrawActionKind.RaiseTo:
				await session.RaiseToAsync(action.Amount
					?? throw new InvalidOperationException("A raise needs an amount."));
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

	private async Task ExpectHoleCardsAsync(FiveCardDrawHandScript script, int dealerSeat)
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

	private async Task ExpectShowdownAsync(FiveCardDrawHandScript script, int dealerSeat)
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

	private async Task StepAsync(FiveCardDrawHandScript script, string where, Func<Task> step)
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
