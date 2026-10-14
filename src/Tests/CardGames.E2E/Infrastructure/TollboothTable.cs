using System.Globalization;
using Microsoft.Playwright;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Logged-in browsers at one Tollbooth table. Plays a <see cref="TollboothHandScript" />
///     through the action panel and the offer overlay, and fails if the app surfaces an exception.
/// </summary>
public sealed class TollboothTable
{
	private readonly string _apiBaseUrl;
	private readonly Guid _gameId;
	private readonly IReadOnlyList<SeededPlayer> _players;
	private readonly int _seatCount;
	private readonly IReadOnlyList<PlayerSession> _sessions;

	private TollboothTable(
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

	public static async Task<TollboothTable> OpenAsync(
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

		return new TollboothTable(apiBaseUrl, gameId, players, sessions);
	}

	public async Task PlayAsync(
		TollboothHandScript script,
		int dealerSeat,
		TollboothLedger ledger,
		bool startGame,
		TollboothHandScript? nextScript = null,
		int nextDealerSeat = 0)
	{
		if (startGame)
		{
			await ScriptAsync(script, dealerSeat);
			await _sessions[0].StartGameAsync();
		}

		var armed = false;
		var folded = new HashSet<HoldEmPosition>();
		foreach (var step in script.Steps)
		{
			var session = Actor(dealerSeat, step.Actor);
			await StepAsync(script, step, async () =>
			{
				if (!armed)
				{
					if (step.Kind == TollboothStepKind.Choose)
					{
						await session.Page.Locator("[data-testid='tollbooth-offer']").WaitForAsync(
							new LocatorWaitForOptions
							{
								State = WaitForSelectorState.Visible,
								Timeout = 60_000
							});
					}
					else
					{
						await session.WaitForActionPanelAsync();
					}

					if (nextScript is not null)
						await ScriptAsync(nextScript, nextDealerSeat);

					await ExpectDealerAsync(dealerSeat);
					await ExpectPotAsync(script.OpeningPot);
					await ExpectStreetBetAsync(
						SeatIndex(dealerSeat, script.BringIn),
						TollboothFiveMaxCatalog.BringInAmount);
					await ExpectDisplayAsync(script.InitialNearest, script.InitialFurthest);
					armed = true;
				}

				if (step.Kind == TollboothStepKind.Choose)
					await ChooseAsync(script, step, session, dealerSeat, folded);
				else
					await BetAsync(script, step, session, dealerSeat);

				if (step.Action == TollboothActionKind.Fold)
					folded.Add(step.Actor);
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

	private async Task ChooseAsync(
		TollboothHandScript script,
		TollboothStep step,
		PlayerSession session,
		int dealerSeat,
		IReadOnlySet<HoldEmPosition> folded)
	{
		var offer = session.Page.Locator("[data-testid='tollbooth-offer']");
		await offer.WaitForAsync(new LocatorWaitForOptions
		{
			State = WaitForSelectorState.Visible,
			Timeout = 60_000
		});

		foreach (var position in folded)
		{
			await Assertions.Expect(Actor(dealerSeat, position).Page.Locator("[data-testid='tollbooth-offer']"))
				.ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 5_000 });
		}

		await ExpectDisplayAsync(step.DisplayNearest!, step.DisplayFurthest!);
		var source = step.Source ?? throw new InvalidOperationException("A choice needs a source.");
		var button = offer.Locator(ButtonFor(source));
		await Assertions.Expect(button).ToContainTextAsync(CostLabel(source));
		await button.EvaluateAsync("element => element.click()");
		await Assertions.Expect(offer).ToBeHiddenAsync(new LocatorAssertionsToBeHiddenOptions { Timeout = 20_000 });

		if (step.PotAfter is int pot)
			await ExpectPotAsync(pot);

		var cards = Seat(session.Page, SeatIndex(dealerSeat, step.Actor))
			.Locator("[data-testid='hole-card'].face-up");
		if (step.FaceDown)
			await ExpectCardAbsentAsync(cards, step.Received!);
		else
			await ExpectCardPresentAsync(cards, step.Received!);
	}

	private async Task BetAsync(
		TollboothHandScript script,
		TollboothStep step,
		PlayerSession session,
		int dealerSeat)
	{
		await session.WaitForActionPanelAsync();
		if (step.UpCards is not null)
			await ExpectUpCardsAsync(script, step.UpCards, dealerSeat);

		switch (step.Action)
		{
			case TollboothActionKind.Fold:
				await session.FoldAsync();
				break;
			case TollboothActionKind.Check:
				await session.CheckAsync();
				break;
			case TollboothActionKind.Call:
				await session.CallAsync(step.CallAmount);
				break;
			case TollboothActionKind.Bet:
				await session.BetAsync(step.Amount ?? throw new InvalidOperationException("A bet needs an amount."));
				break;
			case TollboothActionKind.RaiseTo:
				await session.RaiseToAsync(step.Amount ?? throw new InvalidOperationException("A raise needs an amount."));
				break;
			default:
				throw new InvalidOperationException($"Unexpected action {step.Action}.");
		}

		if (step.PotAfter is int pot)
			await ExpectPotAsync(pot);

		if (step.StreetBet is int streetBet)
			await ExpectStreetBetAsync(SeatIndex(dealerSeat, step.Actor), streetBet);
	}

	private async Task ScriptAsync(TollboothHandScript script, int dealerSeat)
	{
		var cards = TollboothDeckScript.Build(dealerSeat, script);
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

	private async Task ExpectDisplayAsync(string nearest, string furthest)
	{
		await ExpectCardsInOrderAsync(
			Host.Page.Locator("[data-testid='community-card']"),
			[nearest, furthest]);
	}

	private async Task ExpectUpCardsAsync(
		TollboothHandScript script,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> upCards,
		int dealerSeat)
	{
		var checks = upCards.Select(pair =>
			ExpectCardsAsync(
				Seat(Actor(dealerSeat, pair.Key).Page, SeatIndex(dealerSeat, pair.Key))
					.Locator("[data-testid='hole-card'].face-up"),
				pair.Value));
		try
		{
			await Task.WhenAll(checks);
		}
		catch (Exception exception)
		{
			throw new InvalidOperationException($"Up-cards did not match during '{script.Name}'.", exception);
		}
	}

	private async Task ExpectShowdownAsync(TollboothHandScript script, int dealerSeat)
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
		await ExpectDisplayAsync(script.ShowdownNearest, script.ShowdownFurthest);

		if (script.WonByFold)
		{
			await Assertions.Expect(overlay.Locator("[data-testid='showdown-verb']")).ToHaveTextAsync("wins by fold");
			await Assertions.Expect(overlay.Locator(".winner-hand-stage")).ToHaveCountAsync(0);
		}
		else
		{
			await Assertions.Expect(overlay.Locator(".winner-hand-stage")).ToHaveCountAsync(1);
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

	private static async Task ExpectCardPresentAsync(ILocator cards, string expected)
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);
		var last = "";
		while (true)
		{
			var actual = await ReadCardsAsync(cards);
			last = string.Join(" ", actual);
			if (actual.Contains(expected, StringComparer.Ordinal))
				return;

			if (DateTime.UtcNow >= deadline)
				Assert.Fail($"Expected face-up card {expected} but found [{last}].");

			await Task.Delay(150);
		}
	}

	private static async Task ExpectCardAbsentAsync(ILocator cards, string unexpected)
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);
		var last = "";
		while (true)
		{
			var actual = await ReadCardsAsync(cards);
			last = string.Join(" ", actual);
			if (actual.Count > 0 && !actual.Contains(unexpected, StringComparer.Ordinal))
				return;

			if (DateTime.UtcNow >= deadline)
				Assert.Fail($"Face-up cards should not include {unexpected} but found [{last}].");

			await Task.Delay(150);
		}
	}

	private static async Task ExpectCardsAsync(ILocator cards, IReadOnlyList<string> expected)
	{
		var deadline = DateTime.UtcNow.AddSeconds(20);
		var last = "";
		while (true)
		{
			var actual = await ReadCardsAsync(cards);
			last = string.Join(" ", actual);
			var matches = actual.OrderBy(card => card, StringComparer.Ordinal)
				.SequenceEqual(expected.OrderBy(card => card, StringComparer.Ordinal), StringComparer.Ordinal);
			if (matches)
				return;

			if (DateTime.UtcNow >= deadline)
				Assert.Fail($"Expected cards [{string.Join(" ", expected)}] but found [{last}].");

			await Task.Delay(150);
		}
	}

	private static async Task ExpectCardsInOrderAsync(ILocator cards, IReadOnlyList<string> expected)
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
				Assert.Fail($"Expected cards [{string.Join(" ", expected)}] in order but found [{last}].");

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

	private async Task StepAsync(TollboothHandScript script, TollboothStep? step, Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (Exception exception)
		{
			var where = step is null
				? "showdown"
				: step.Kind == TollboothStepKind.Choose
					? $"{step.Actor} {step.Source}"
					: $"{step.Actor} {step.Action}";
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

	private static string ButtonFor(TollboothSource source) => source switch
	{
		TollboothSource.Furthest => "[data-testid='tollbooth-furthest']",
		TollboothSource.Nearest => "[data-testid='tollbooth-nearest']",
		TollboothSource.Deck => "[data-testid='tollbooth-deck']",
		_ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown Tollbooth choice.")
	};

	private static string CostLabel(TollboothSource source) => source switch
	{
		TollboothSource.Furthest => "Free",
		TollboothSource.Nearest => $"{TollboothFiveMaxCatalog.Ante.ToString(CultureInfo.InvariantCulture)} chips",
		TollboothSource.Deck => $"{(TollboothFiveMaxCatalog.Ante * 2).ToString(CultureInfo.InvariantCulture)} chips",
		_ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown Tollbooth choice.")
	};

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
