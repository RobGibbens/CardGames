namespace CardGames.E2E;

/// <summary>
///     Playwright end-to-end test. Set <c>CARDGAMES_E2E_HEADED=1</c> to show browser windows.
///     Install browsers once after build:
///     <c>pwsh src/Tests/CardGames.E2E/bin/Debug/net11.0/playwright.ps1 install chromium</c>.
///     The fixture also installs Chromium the first time a test actually runs.
/// </summary>
public sealed class E2EFactAttribute : FactAttribute
{
	public E2EFactAttribute(int timeout = 0)
	{
		if (timeout > 0)
			Timeout = timeout;
	}
}
