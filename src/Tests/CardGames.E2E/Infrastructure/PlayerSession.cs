using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     One player: an isolated browser context, Identity login, and the table page.
/// </summary>
public sealed class PlayerSession : IAsyncDisposable
{
	private readonly IBrowserContext _context;
	private readonly Lock _errorGate = new();
	private readonly List<string> _pageErrors = [];
	private readonly List<string> _consoleErrors = [];
	private bool _traceFinished;

	private PlayerSession(IBrowserContext context, IPage page, SeededPlayer player)
	{
		_context = context;
		Page = page;
		Player = player;
		page.PageError += (_, error) => AddError(_pageErrors, error);
		page.Console += (_, message) =>
		{
			if (string.Equals(message.Type, "error", StringComparison.OrdinalIgnoreCase))
				AddError(_consoleErrors, message.Text);
		};
	}

	public IReadOnlyList<string> PageErrors
	{
		get
		{
			lock (_errorGate)
				return _pageErrors.ToArray();
		}
	}

	public IReadOnlyList<string> ConsoleErrors
	{
		get
		{
			lock (_errorGate)
				return _consoleErrors.ToArray();
		}
	}

	private void AddError(List<string> errors, string error)
	{
		lock (_errorGate)
			errors.Add(error);
	}

	public IPage Page { get; }

	public SeededPlayer Player { get; }

	public static async Task<PlayerSession> StartAsync(IBrowser browser, string webBaseUrl, SeededPlayer player)
	{
		var context = await browser.NewContextAsync(new BrowserNewContextOptions
		{
			BaseURL = webBaseUrl,
			IgnoreHTTPSErrors = true
		});
		await context.Tracing.StartAsync(new TracingStartOptions
		{
			Screenshots = true,
			Snapshots = true,
			Sources = true
		});
		var page = await context.NewPageAsync();
		return new PlayerSession(context, page, player);
	}

	public async Task LoginAsync()
	{
		await Page.GotoAsync("/Account/Login");
		await Page.Locator("[id='Input.Email']").FillAsync(Player.Email);
		await Page.Locator("[id='Input.Password']").FillAsync(Player.Password);
		await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Log in with email" }).ClickAsync();
		await Page.WaitForURLAsync(
			url => !url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase),
			new PageWaitForURLOptions { Timeout = 30_000 });
	}

	public async Task OpenTableAsync(Guid gameId, bool autoJoin = false)
	{
		var path = autoJoin ? $"/table/{gameId}?autojoin=1" : $"/table/{gameId}";
		await Page.GotoAsync(path);
		await Page.Locator("[data-testid='seat']").First.WaitForAsync(new LocatorWaitForOptions
		{
			Timeout = 30_000
		});
	}

	public async Task ConfirmBuyInAsync()
	{
		var submit = Page.Locator("[data-testid='join-buy-in-submit']");
		await submit.WaitForAsync(new LocatorWaitForOptions
		{
			State = WaitForSelectorState.Visible,
			Timeout = 30_000
		});
		await submit.ClickAsync();
		await ExpectSeatedAsync(Player.DisplayName, 15_000);
		await WaitForHubConnectionAsync();
	}

	public Task WaitForHubConnectionAsync()
	{
		return Assertions.Expect(Page.Locator("[data-testid='connection-indicator'][data-connected='true']"))
			.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });
	}

	public Task ExpectSeatedAsync(string displayName, int timeoutMs = 10_000)
	{
		return Assertions.Expect(Page.Locator("[data-testid='seat-player-name']")
				.Filter(new LocatorFilterOptions { HasText = displayName }))
			.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = timeoutMs });
	}

	public async Task StartGameAsync()
	{
		var button = Page.Locator("[data-testid='start-game']");
		await Assertions.Expect(button).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions
		{
			Timeout = 30_000
		});
		await button.ClickAsync();
	}

	public async Task<ILocator> WaitForActionPanelAsync()
	{
		var panel = Page.Locator("[data-testid='action-panel']");
		try
		{
			await panel.WaitForAsync(new LocatorWaitForOptions
			{
				State = WaitForSelectorState.Visible,
				Timeout = 60_000
			});
		}
		catch (TimeoutException)
		{
			var activeSeat = await Page.Locator(".seat-active-turn").GetAttributeAsync("data-seat-index");
			var phase = await Page.Locator(".phase-label, .table-phase, [data-testid='phase']").AllInnerTextsAsync();
			var toasts = await Page.Locator(".toast, .toast-message").AllInnerTextsAsync();
			var pot = await Page.Locator("[data-testid='pot-amount']").AllInnerTextsAsync();
			var bets = await Page.Locator("[data-testid='seat-bet']").AllInnerTextsAsync();
			var showdown = await Page.Locator("[aria-label='Showdown results']").CountAsync();
			throw new TimeoutException(
				$"Action panel not visible for {Player.Email}. " +
				$"activeSeat={activeSeat ?? "(none)"} pot=[{string.Join("|", pot)}] " +
				$"bets=[{string.Join("|", bets)}] showdown={showdown} " +
				$"phase=[{string.Join("|", phase)}] toasts=[{string.Join("|", toasts)}]");
		}

		return panel;
	}

	public async Task FoldAsync()
	{
		var panel = await WaitForActionPanelAsync();
		await ClickUntilDismissedAsync(panel.Locator("[data-testid='action-fold']"));
	}

	public async Task CheckAsync()
	{
		var panel = await WaitForActionPanelAsync();
		await ClickUntilDismissedAsync(panel.Locator("[data-testid='action-check']"));
	}

	public async Task CallAsync(int? amount = null)
	{
		var panel = await WaitForActionPanelAsync();
		var button = panel.Locator("[data-testid='action-call']");
		if (amount is int expected)
		{
			await Assertions.Expect(button.Locator(".action-label")).ToHaveTextAsync(
				$"Call {expected.ToString(CultureInfo.InvariantCulture)}",
				new LocatorAssertionsToHaveTextOptions { Timeout = 15_000 });
		}

		await ClickUntilDismissedAsync(button);
	}

	public async Task BetAsync(int amount)
	{
		var panel = await WaitForActionPanelAsync();
		await SetBetAmountAsync(panel, amount);
		var button = panel.Locator("[data-testid='action-bet']");
		await Assertions.Expect(button.Locator(".action-label")).ToHaveTextAsync(
			$"Bet {amount.ToString(CultureInfo.InvariantCulture)}",
			new LocatorAssertionsToHaveTextOptions { Timeout = 15_000 });
		await ClickUntilDismissedAsync(button);
	}

	public async Task RaiseToAsync(int amount)
	{
		var panel = await WaitForActionPanelAsync();
		await SetBetAmountAsync(panel, amount);
		var button = panel.Locator("[data-testid='action-raise']");
		await Assertions.Expect(button.Locator(".action-label")).ToHaveTextAsync(
			$"Raise to {amount.ToString(CultureInfo.InvariantCulture)}",
			new LocatorAssertionsToHaveTextOptions { Timeout = 15_000 });
		await ClickUntilDismissedAsync(button);
	}

	public async Task AllInAsync()
	{
		var panel = await WaitForActionPanelAsync();
		await ClickUntilDismissedAsync(panel.Locator("[data-testid='action-all-in']"));
	}

	/// <summary>
	///     Picks one hole card in the Choose Showcase panel. The panel sorts by rank, so the
	///     click targets the card face and the original hole index travels with that element.
	/// </summary>
	public async Task ChooseShowcaseAsync(string cardCode)
	{
		var panel = Page.Locator(".draw-panel");
		await panel.WaitForAsync(new LocatorWaitForOptions
		{
			State = WaitForSelectorState.Visible,
			Timeout = 45_000
		});
		await Assertions.Expect(panel.Locator(".draw-panel-title")).ToContainTextAsync(
			"Choose Showcase",
			new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
		await Assertions.Expect(panel.Locator(".draw-card-wrapper")).ToHaveCountAsync(
			5,
			new LocatorAssertionsToHaveCountOptions { Timeout = 15_000 });

		var (rank, suit) = DrawPanelFace(cardCode);
		var card = panel.Locator(
			$".draw-card-wrapper:has(playing-card[rank='{rank}'][suit='{suit}'])");
		await card.EvaluateAsync("element => element.click()");

		var choose = panel.Locator(".discard-btn");
		await Assertions.Expect(choose).ToContainTextAsync(
			"Choose 1",
			new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
		await choose.EvaluateAsync("element => element.click()");
		await Assertions.Expect(panel).ToBeHiddenAsync(
			new LocatorAssertionsToBeHiddenOptions { Timeout = 20_000 });
	}

	private static (string Rank, string Suit) DrawPanelFace(string cardCode)
	{
		var rank = char.ToUpperInvariant(cardCode[0]) switch
		{
			'A' => "Ace",
			'K' => "King",
			'Q' => "Queen",
			'J' => "Jack",
			'T' => "10",
			_ => cardCode[0].ToString()
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

	public async Task DecideBuyCardAsync(bool accept, int price)
	{
		var button = Page.Locator(accept
			? "[data-testid='buy-card-accept']"
			: "[data-testid='buy-card-decline']");
		await button.WaitForAsync(new LocatorWaitForOptions
		{
			State = WaitForSelectorState.Visible,
			Timeout = 30_000
		});
		await Assertions.Expect(Page.Locator("[data-testid='buy-card-price']")).ToContainTextAsync(
			price.ToString(CultureInfo.InvariantCulture),
			new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
		await button.EvaluateAsync("element => element.click()");
		await Assertions.Expect(Page.Locator(".buy-card-panel")).ToBeHiddenAsync(
			new LocatorAssertionsToBeHiddenOptions { Timeout = 20_000 });
	}

	/// <summary>
	///     The action panel is scaled and fades in, so a mouse click on that box often never
	///     reaches the Blazor handler. Dispatch one click and wait until the action is accepted.
	///     The button leaves when the turn moves. It stays when this player closes the street
	///     and is first to act on the next one, so a new community card or stud up-card counts
	///     as acceptance too. A showdown that appears after the click does as well.
	///     A second click is rejected once the first action has already moved the turn.
	/// </summary>
	private async Task ClickUntilDismissedAsync(ILocator button)
	{
		var testId = await button.GetAttributeAsync("data-testid") ?? "action";
		var community = Page.Locator("[data-testid='community-card']");
		var studUpCards = Page.Locator("[data-testid='hole-card'].face-up");
		var showdown = Page.Locator("[aria-label='Showdown results']");
		var communityBefore = await community.CountAsync();
		var studBefore = await studUpCards.CountAsync();
		var showdownBefore = await showdown.IsVisibleAsync();
		await button.EvaluateAsync("element => element.click()");

		var deadline = DateTime.UtcNow.AddSeconds(20);
		while (DateTime.UtcNow < deadline)
		{
			if (!await button.IsVisibleAsync())
				return;

			if (await community.CountAsync() > communityBefore)
				return;

			if (await studUpCards.CountAsync() > studBefore)
				return;

			if (!showdownBefore && await showdown.IsVisibleAsync())
				return;

			await Task.Delay(100);
		}

		var toasts = await Page.Locator(".toast-message").AllInnerTextsAsync();
		throw new TimeoutException(
			$"'{testId}' was still available for {Player.Email} after the click. " +
			$"toasts=[{string.Join("|", toasts)}]");
	}

	public async Task CloseShowdownAsync()
	{
		var close = Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Close showdown" });
		if (await close.CountAsync() == 0 || !await close.IsVisibleAsync())
			return;

		await close.ClickAsync();
	}

	private static async Task SetBetAmountAsync(ILocator panel, int amount)
	{
		var input = panel.Locator("[data-testid='bet-amount']");
		await input.FillAsync(amount.ToString(CultureInfo.InvariantCulture));
		await input.EvaluateAsync("el => el.dispatchEvent(new Event('change', { bubbles: true }))");
	}

	public async Task SaveTraceAsync(string testName, string traceDirectory)
	{
		if (_traceFinished)
			return;

		Directory.CreateDirectory(traceDirectory);
		var safeEmail = Player.Email.Replace('@', '_');
		var path = Path.Combine(traceDirectory, $"{testName}-{safeEmail}.zip");
		await _context.Tracing.StopAsync(new TracingStopOptions { Path = path });
		_traceFinished = true;
	}

	public async ValueTask DisposeAsync()
	{
		if (!_traceFinished)
		{
			try
			{
				await _context.Tracing.StopAsync();
			}
			catch (PlaywrightException)
			{
				// Tracing was never started, or the browser is already gone.
			}

			_traceFinished = true;
		}

		await _context.CloseAsync();
	}
}

public abstract class E2ETestBase : IAsyncLifetime
{
	private readonly List<PlayerSession> _sessions = [];
	private ScenarioSeeder? _seeder;

	protected E2ETestBase(PlaywrightAppFixture fixture)
	{
		Fixture = fixture;
	}

	protected PlaywrightAppFixture Fixture { get; }

	protected ScenarioSeeder Seeder =>
		_seeder ?? throw new InvalidOperationException("The scenario seeder is not ready.");

	public async Task InitializeAsync()
	{
		if (!E2EFactAttribute.IsEnabled)
			return;

		await Fixture.EnsureStartedAsync();
		await Fixture.ResetDatabaseAsync();
		_seeder = new ScenarioSeeder(Fixture.ConnectionString);
	}

	public async Task DisposeAsync()
	{
		foreach (var session in _sessions)
			await session.DisposeAsync();

		_sessions.Clear();

		if (_seeder is not null)
			await _seeder.DisposeAsync();
	}

	protected async Task<PlayerSession> LoginPlayerAsync(SeededPlayer player)
	{
		var session = await PlayerSession.StartAsync(Fixture.Browser, Fixture.WebBaseUrl, player);
		_sessions.Add(session);
		await session.LoginAsync();
		return session;
	}

	protected async Task ExecuteAsync(Func<Task> scenario, [CallerMemberName] string testName = "")
	{
		try
		{
			await scenario();
		}
		catch (Exception)
		{
			foreach (var session in _sessions)
				await session.SaveTraceAsync(testName, Fixture.TraceDirectory);

			throw;
		}
	}
}
