namespace CardGames.E2E;

/// <summary>
///     Skips the test unless <c>CARDGAMES_E2E=1</c>. Set <c>CARDGAMES_E2E_HEADED=1</c> to show browser windows.
///     Install browsers once after build:
///     <c>pwsh src/Tests/CardGames.E2E/bin/Debug/net10.0/playwright.ps1 install chromium</c>.
///     The fixture also installs Chromium the first time a test actually runs.
/// </summary>
public sealed class E2EFactAttribute : FactAttribute
{
	public const string EnvironmentVariableName = "CARDGAMES_E2E";

	public E2EFactAttribute()
	{
		if (!IsEnabled)
		{
			Skip =
				$"Set {EnvironmentVariableName}=1 to run Playwright end-to-end tests. " +
				"Install browsers with: pwsh src/Tests/CardGames.E2E/bin/Debug/net10.0/playwright.ps1 install chromium";
		}
	}

	public static bool IsEnabled =>
		string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariableName), "1", StringComparison.Ordinal);
}
