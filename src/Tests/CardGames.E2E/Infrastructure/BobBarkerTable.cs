using System.Globalization;
using Microsoft.Playwright;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Logged-in browsers at one Bob Barker table. Chooses showcases, plays a
///     <see cref="BobBarkerHandScript" /> through the action panel, and checks the board, pot, and split.
/// </summary>
public sealed class BobBarkerTable
{
	private readonly string _apiBaseUrl;
	private readonly Guid _gameId;
	private readonly IReadOnlyList<SeededPlayer> _players;
	private readonly int _seatCount;
	private readonly IReadOnlyList<PlayerSession> _sessions;

	private BobBarkerTable(
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

	public static async Task<BobBarkerTable> OpenAsync(
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

		return new BobBarkerTable(apiBaseUrl, gameId, players, sessions);
	}

	public async Task PlayAsync(
		BobBarkerHandScript script,
		int dealerSeat,
		BobBarkerLedger ledger,
		bool startGame,
		BobBarkerHandScript? nextScript = null,
		int nextDealerSeat = 0)
	{
		if (startGame)
		{
			await ScriptAsync(script, dealerSeat);
			await _sessions[0].StartGameAsync();
		}

		await StepAsync(script, null, () => ChooseShowcasesAsync(script, dealerSeat), "showcase");

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

	/// <summary>
	///     Fails when a seated browser recorded an uncaught error, a console error, or the Blazor error UI.
	/// </summary>
	public async Task ExpectNoAppExceptionsAsync()
	{
		var problems = await CollectAppExceptionsAsync();
		if (problems.Count > 0)
			Assert.Fail(string.Join(Environment.NewLine, problems));
	}

	private async Task ChooseShowcasesAsync(BobBarkerHandScript script, int dealerSeat)
	{
		await Task.WhenAll(script.Showcases.Select(pair =>
			Actor(dealerSeat, pair.Key).ChooseShowcaseAsync(pair.Value)));
	}

	private async Task ScriptAsync(BobBarkerHandScript script, int dealerSeat)
	{
		var holes = script.HoleCards.ToDictionary(
			pair => SeatIndex(dealerSeat, pair.Key),
			pair => pair.Value);
		var cards = BobBarkerDeckScript.Build(dealerSeat, holes, script.DealerCard, script.Board);
		await ScriptedDeckClient.SetAsync(_apiBaseUrl, _gameId, cards);
	}

	private int SeatIndex(int dealerSeat, HoldEmPosition position) =>
		HoldEmSeats.Index(_seatCount, dealerSeat, position);

	private PlayerSession Actor(int dealerSeat, HoldEmPosition position) =>
		_sessions[SeatIndex(dealerSeat, position)];

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
		var smallBlind = SeatIndex(dealerSeat, HoldEmPosition.SmallBlind);
		var bigBlind = SeatIndex(dealerSeat, HoldEmPosition.BigBlind);

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

	private async Task ExpectHoleCardsAsync(BobBarkerHandScript script, int dealerSeat)
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

	private async Task ExpectShowdownAsync(BobBarkerHandScript script, int dealerSeat)
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

		var showcase = overlay.Locator("[aria-label='Bob Barker showcase results']");
		await Assertions.Expect(showcase).ToBeVisibleAsync(
			new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });
		var (dealerRank, dealerSuit) = ShowdownFace(script.DealerCard);
		var dealerCard = showcase.Locator(".bob-barker-showcase-item-dealer playing-card");
		await Assertions.Expect(dealerCard).ToHaveAttributeAsync("rank", dealerRank);
		await Assertions.Expect(dealerCard).ToHaveAttributeAsync("suit", dealerSuit);

		var playerRows = showcase.Locator(".bob-barker-showcase-item:not(.bob-barker-showcase-item-dealer)");
		if (script.WonByFold)
		{
			await Assertions.Expect(playerRows).ToHaveCountAsync(0);
			return;
		}

		var live = LivePositions(script).ToArray();
		await Assertions.Expect(playerRows).ToHaveCountAsync(live.Length);
		await Assertions.Expect(playerRows.First.Locator(".other-hand-name")).ToHaveTextAsync(
			_players[SeatIndex(dealerSeat, script.ShowcaseWinner!.Value)].DisplayName);

		foreach (var position in live)
		{
			var row = playerRows.Filter(new LocatorFilterOptions
			{
				HasText = _players[SeatIndex(dealerSeat, position)].DisplayName
			});
			var (rank, suit) = ShowdownFace(script.Showcases[position]);
			var card = row.Locator("playing-card");
			await Assertions.Expect(card).ToHaveAttributeAsync("rank", rank);
			await Assertions.Expect(card).ToHaveAttributeAsync("suit", suit);
		}
	}

	private static IEnumerable<HoldEmPosition> LivePositions(BobBarkerHandScript script)
	{
		var folded = script.Actions
			.Where(action => action.Kind == HoldEmActionKind.Fold)
			.Select(action => action.Actor)
			.ToHashSet();
		return script.HoleCards.Keys.Where(position => !folded.Contains(position));
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

	private async Task StepAsync(
		BobBarkerHandScript script,
		HoldEmAction? action,
		Func<Task> step,
		string? stage = null)
	{
		try
		{
			await step();
		}
		catch (Exception exception)
		{
			var where = stage
			            ?? (action is null
				            ? "showdown"
				            : $"{action.Actor} {action.Kind}");
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

	private static (string Rank, string Suit) ShowdownFace(string cardCode)
	{
		var rank = char.ToUpperInvariant(cardCode[0]) switch
		{
			'T' => "10",
			_ => char.ToUpperInvariant(cardCode[0]).ToString()
		};
		var suit = char.ToLowerInvariant(cardCode[1]) switch
		{
			'h' => "Hearts",
			'd' => "Diamonds",
			's' => "Spades",
			'c' => "Clubs",
			_ => throw new InvalidOperationException($"Unexpected card '{cardCode}'.")
		};
		return (rank, suit);
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
