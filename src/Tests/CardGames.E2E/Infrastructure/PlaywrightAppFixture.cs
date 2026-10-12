using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using Respawn;
using Respawn.Graph;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Starts the Aspire stack and one Chromium browser for the whole run.
///     Database reset is per test and does not recycle the host.
/// </summary>
public sealed class PlaywrightAppFixture : IAsyncLifetime
{
	public const string SigningKey = "e2e-only-internal-identity-signing-key-32";

	/// <summary>
	///     When set to <c>1</c>, Chromium runs with a visible window (<c>Headless = false</c>).
	/// </summary>
	public const string HeadedEnvironmentVariableName = "CARDGAMES_E2E_HEADED";

	public static bool RunHeaded =>
		string.Equals(Environment.GetEnvironmentVariable(HeadedEnvironmentVariableName), "1", StringComparison.Ordinal);

	private readonly SemaphoreSlim _gate = new(1, 1);
	private DistributedApplication? _app;
	private IPlaywright? _playwright;
	private Respawner? _respawner;
	private bool _started;

	public string WebBaseUrl { get; private set; } = "";

	public string ConnectionString { get; private set; } = "";

	public IBrowser Browser { get; private set; } = null!;

	public string TraceDirectory { get; } =
		Path.Combine(AppContext.BaseDirectory, "TestResults", "traces");

	public Task InitializeAsync() => Task.CompletedTask;

	public async Task DisposeAsync()
	{
		if (Browser is not null)
			await Browser.CloseAsync();

		_playwright?.Dispose();

		if (_app is not null)
			await _app.DisposeAsync();

		_gate.Dispose();
	}

	public async Task EnsureStartedAsync()
	{
		if (_started)
			return;

		await _gate.WaitAsync();
		try
		{
			if (_started)
				return;

			await StartCoreAsync();
			_started = true;
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task ResetDatabaseAsync()
	{
		if (_respawner is null || string.IsNullOrWhiteSpace(ConnectionString))
			throw new InvalidOperationException("The end-to-end host is not started.");

		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync();
		await _respawner.ResetAsync(connection);
	}

	private async Task StartCoreAsync()
	{
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CardGames_AppHost>();
		builder.Configuration["Parameters:internalIdentitySigningKey"] = SigningKey;

		DistributedApplication? app = null;
		try
		{
			app = await builder.BuildAsync();
			await app.StartAsync();

			using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(10));
			foreach (var resource in new[] { "sqlserver", "cache", "storage", "messaging", "api", "web" })
			{
				await app.ResourceNotifications.WaitForResourceHealthyAsync(resource, startup.Token);
			}

			var endpoint = ResolveWebEndpoint(app);
			var connectionString = await app.GetConnectionStringAsync("cardsdb", startup.Token);
			if (string.IsNullOrWhiteSpace(connectionString))
				throw new InvalidOperationException("Aspire did not publish a cardsdb connection string.");

			await using (var connection = new SqlConnection(connectionString))
			{
				await connection.OpenAsync(startup.Token);
				_respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
				{
					DbAdapter = DbAdapter.SqlServer,
					TablesToIgnore = [new Table("__EFMigrationsHistory")]
				});
			}

			var installExitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
			if (installExitCode != 0)
				throw new InvalidOperationException($"Playwright browser install failed with exit code {installExitCode}.");

			_playwright = await Microsoft.Playwright.Playwright.CreateAsync();
			var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
			{
				Headless = !RunHeaded,
				SlowMo = RunHeaded ? 50 : 0
			});

			_app = app;
			Browser = browser;
			ConnectionString = connectionString;
			WebBaseUrl = endpoint.ToString().TrimEnd('/');
			Directory.CreateDirectory(TraceDirectory);
			Console.WriteLine($"Playwright E2E web: {WebBaseUrl}");
			app = null;
		}
		finally
		{
			if (app is not null)
				await app.DisposeAsync();
		}
	}

	private static Uri ResolveWebEndpoint(DistributedApplication app)
	{
		try
		{
			return app.GetEndpoint("web", "https");
		}
		catch (Exception)
		{
			return app.GetEndpoint("web", "http");
		}
	}
}
