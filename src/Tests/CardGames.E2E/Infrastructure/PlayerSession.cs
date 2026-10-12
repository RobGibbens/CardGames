using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     One player: an isolated browser context, Identity login, and the table page.
/// </summary>
public sealed class PlayerSession : IAsyncDisposable
{
	private readonly IBrowserContext _context;
	private bool _traceFinished;

	private PlayerSession(IBrowserContext context, IPage page, SeededPlayer player)
	{
		_context = context;
		Page = page;
		Player = player;
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
