using System.Globalization;
using Microsoft.Playwright;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Logged-in browsers at one Screw Your Neighbor table.
///     One scripted deck is posted before the first hand. Later hands keep the undealt remainder.
/// </summary>
public sealed class ScrewYourNeighborTable
{
	private readonly string _apiBaseUrl;
	private readonly Guid _gameId;
	private readonly IReadOnlyList<PlayerSession> _sessions;

	private ScrewYourNeighborTable(
		string apiBaseUrl,
		Guid gameId,
		IReadOnlyList<PlayerSession> sessions)
	{
		_apiBaseUrl = apiBaseUrl;
		_gameId = gameId;
		_sessions = sessions;
	}

	public static async Task<ScrewYourNeighborTable> OpenAsync(
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

		return new ScrewYourNeighborTable(apiBaseUrl, gameId, sessions);
	}

	public async Task PlayAsync(
		IReadOnlyList<ScrewYourNeighborHandScript> hands,
		string nextCard)
	{
		await ScriptedDeckClient.SetAsync(
			_apiBaseUrl,
			_gameId,
			ScrewYourNeighborDeckScript.Build(hands, nextCard));
		await Host.StartGameAsync();

		var stacks = Enumerable.Repeat(ScrewYourNeighborFiveMaxCatalog.BuyIn, _sessions.Count).ToArray();
		var pot = 0;
		foreach (var hand in hands)
		{
			await PlayHandAsync(hand, stacks, pot);
			stacks = hand.StacksAfter.ToArray();
			pot = hand.PotAfter;
		}

		await ExpectContinuingHandAsync(
			dealerSeat: hands[^1].NextDealerSeat,
			actorSeat: (hands[^1].NextDealerSeat + 1) % _sessions.Count,
			card: nextCard,
			stacks,
			pot);
		await ExpectNoNewDeckToastAsync();
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

	private PlayerSession Host => _sessions[0];

	private async Task PlayHandAsync(ScrewYourNeighborHandScript hand, IReadOnlyList<int> stacks, int pot)
	{
		await StepAsync(hand, "opening", async () =>
		{
			await WaitForTurnAsync(hand.Actions[0]);
			await ExpectDealerAsync(hand.DealerSeat);
			await ExpectPotAsync(pot);
			await ExpectStacksAsync(stacks);
			await ExpectKingsVisibleAsync(hand);
			await ExpectSkippedPlayersHaveNoTurnAsync(hand);
		});

		for (var index = 0; index < hand.Actions.Count; index++)
		{
			var action = hand.Actions[index];
			var isLast = index == hand.Actions.Count - 1;
			await StepAsync(hand, $"seat {action.Seat} {action.Decision}", async () =>
			{
				await WaitForTurnAsync(action);
				await ExpectSkippedPlayersHaveNoTurnAsync(hand);
				await ExpectShownCardAsync(action);
				await ExpectTradeButtonAsync(action);
				await SubmitAsync(action);

				if (isLast)
					await ExpectFinalCardsAsync(hand);
			});
		}

		await ExpectNoNewDeckToastAsync();
		await ExpectNoAppExceptionsAsync();
	}

	private async Task ExpectContinuingHandAsync(
		int dealerSeat,
		int actorSeat,
		string card,
		IReadOnlyList<int> stacks,
		int pot)
	{
		var action = new ScrewYourNeighborAction(actorSeat, ScrewYourNeighborDecision.Keep, card, TradeVisible: true);
		await WaitForTurnAsync(action);
		await ExpectShownCardAsync(action);
		await ExpectDealerAsync(dealerSeat);
		await ExpectPotAsync(pot);
		await ExpectStacksAsync(stacks);
	}

	private async Task WaitForTurnAsync(ScrewYourNeighborAction action)
	{
		await _sessions[action.Seat].Page.Locator(".keep-btn").WaitForAsync(new LocatorWaitForOptions
		{
			State = WaitForSelectorState.Visible,
			Timeout = 20_000
		});
	}

	private async Task ExpectShownCardAsync(ScrewYourNeighborAction action)
	{
		var card = _sessions[action.Seat].Page.Locator(".keep-or-trade-card playing-card");
		var deadline = DateTime.UtcNow.AddSeconds(10);
		var last = "";
		while (true)
		{
			var rank = await card.GetAttributeAsync("rank");
			var suit = await card.GetAttributeAsync("suit");
			last = $"{rank} {suit}";
			if (!string.IsNullOrWhiteSpace(rank) && !string.IsNullOrWhiteSpace(suit) &&
			    ToCode(rank, suit) == action.CardShown)
				return;

			if (DateTime.UtcNow >= deadline)
				Assert.Fail($"Seat {action.Seat} should see {action.CardShown} but last saw '{last}'.");

			await Task.Delay(100);
		}
	}

	private async Task ExpectTradeButtonAsync(ScrewYourNeighborAction action)
	{
		var trade = _sessions[action.Seat].Page.Locator(".trade-btn");
		if (action.TradeVisible)
		{
			await Assertions.Expect(trade).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
			{
				Timeout = 10_000
			});
			return;
		}

		await Assertions.Expect(trade).ToHaveCountAsync(0);
	}

	private async Task SubmitAsync(ScrewYourNeighborAction action)
	{
		var selector = action.Decision == ScrewYourNeighborDecision.Trade ? ".trade-btn" : ".keep-btn";
		await _sessions[action.Seat].Page.Locator(selector).ClickAsync();
	}

	private async Task ExpectKingsVisibleAsync(ScrewYourNeighborHandScript hand)
	{
		foreach (var (seat, card) in hand.DealtCards)
		{
			if (card[0] != 'K')
				continue;

			var suit = SuitName(card);
			foreach (var session in _sessions)
			{
				var king = Seat(session.Page, seat).Locator(
					$".card-shell.face-up[data-testid='hole-card'][data-rank='K'][data-suit='{suit}'], playing-card[rank='King'][suit='{suit}']");
				await Assertions.Expect(king.First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
				{
					Timeout = 20_000
				});
			}
		}
	}

	private async Task ExpectSkippedPlayersHaveNoTurnAsync(ScrewYourNeighborHandScript hand)
	{
		foreach (var seat in hand.SkippedSeats)
		{
			await Assertions.Expect(_sessions[seat].Page.Locator(".keep-btn")).ToHaveCountAsync(
				0,
				new LocatorAssertionsToHaveCountOptions { Timeout = 5_000 });
		}
	}

	private async Task ExpectFinalCardsAsync(ScrewYourNeighborHandScript hand)
	{
		var deadline = DateTime.UtcNow.AddSeconds(4);
		var last = "";
		while (true)
		{
			var actual = new Dictionary<int, string>();
			for (var seat = 0; seat < _sessions.Count; seat++)
			{
				var code = await ReadOwnFaceUpCardAsync(_sessions[seat].Page, seat);
				if (code is null)
					break;

				actual[seat] = code;
			}

			last = string.Join(" ", actual.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
			if (actual.Count == hand.FinalCards.Count &&
			    hand.FinalCards.All(pair => actual.TryGetValue(pair.Key, out var code) && code == pair.Value))
				return;

			if (DateTime.UtcNow >= deadline)
			{
				Assert.Fail(
					$"Hand '{hand.Name}' expected final cards [{FormatCards(hand.FinalCards)}] but last saw [{last}].");
			}

			await Task.Delay(100);
		}
	}

	private static async Task<string?> ReadOwnFaceUpCardAsync(IPage page, int seat)
	{
		var card = Seat(page, seat).Locator(".card-shell.face-up[data-testid='hole-card']");
		if (await card.CountAsync() == 0)
			return null;

		var rank = await card.First.GetAttributeAsync("data-rank");
		var suit = await card.First.GetAttributeAsync("data-suit");
		if (string.IsNullOrWhiteSpace(rank) || string.IsNullOrWhiteSpace(suit))
			return null;

		return ToCode(rank, suit);
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

	private async Task ExpectStacksAsync(IReadOnlyList<int> stacks)
	{
		var deadline = DateTime.UtcNow.AddSeconds(8);
		var last = "";
		while (true)
		{
			var actual = new int[stacks.Count];
			var ready = true;
			for (var seat = 0; seat < stacks.Count; seat++)
			{
				actual[seat] = await Seat(Host.Page, seat).Locator(".syn-chip-active").CountAsync();
				if (actual[seat] != stacks[seat] / ScrewYourNeighborFiveMaxCatalog.StackSize)
					ready = false;
			}

			last = string.Join(", ", actual);
			if (ready)
				return;

			if (DateTime.UtcNow >= deadline)
			{
				Assert.Fail(
					$"Expected stack chips [{string.Join(", ", stacks.Select(stack => stack / ScrewYourNeighborFiveMaxCatalog.StackSize))}] but last saw [{last}].");
			}

			await Task.Delay(150);
		}
	}

	private Task ExpectPotAsync(int amount) =>
		ExpectAmountAsync(Host.Page.Locator("[data-testid='pot-amount']"), amount);

	private async Task ExpectNoNewDeckToastAsync()
	{
		foreach (var session in _sessions)
		{
			var toasts = session.Page.Locator(".toast-message");
			var count = await toasts.CountAsync();
			for (var index = 0; index < count; index++)
			{
				var text = await toasts.Nth(index).InnerTextAsync();
				if (text.Contains("Starting new deck", StringComparison.OrdinalIgnoreCase))
					Assert.Fail($"{session.Player.Email} showed a new-deck toast: {text}");
			}
		}
	}

	private static ILocator Seat(IPage page, int seat) =>
		page.Locator($"[data-testid='seat'][data-seat-index='{seat.ToString(CultureInfo.InvariantCulture)}']");

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

	private async Task StepAsync(ScrewYourNeighborHandScript hand, string where, Func<Task> step)
	{
		try
		{
			await step();
			await ExpectNoAppExceptionsAsync();
		}
		catch (Exception exception)
		{
			var problems = await CollectAppExceptionsAsync();
			var app = problems.Count == 0
				? ""
				: $" App exceptions: {string.Join(" | ", problems)}";
			throw new InvalidOperationException($"Hand '{hand.Name}' failed during {where}.{app}", exception);
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

	private static string FormatCards(IReadOnlyDictionary<int, string> cards) =>
		string.Join(" ", cards.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));

	private static string SuitName(string card) =>
		card[1] switch
		{
			'h' => "Hearts",
			'd' => "Diamonds",
			's' => "Spades",
			'c' => "Clubs",
			_ => throw new InvalidOperationException($"Unexpected suit in '{card}'.")
		};

	private static string ToCode(string rank, string suit)
	{
		var normalizedRank = rank.ToUpperInvariant() switch
		{
			"10" => "T",
			"JACK" => "J",
			"QUEEN" => "Q",
			"KING" => "K",
			"ACE" => "A",
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
