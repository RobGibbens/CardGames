using System.Collections.Immutable;
using Microsoft.AspNetCore.Hosting;

namespace CardGames.Poker.Web.Components.Shared;

internal static class SiteThemeCatalog
{
	private const string DefaultThemeFileName = "astrovista.css";
	private const string ThemeCssDirectory = "css";

	public static string DefaultTheme => DefaultThemeFileName;

	public static IReadOnlyList<string> GetAvailableThemes(IWebHostEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment);

		var webRootPath = environment.WebRootPath;
		if (string.IsNullOrWhiteSpace(webRootPath))
		{
			return [DefaultThemeFileName];
		}

		var themeDirectoryPath = Path.Combine(webRootPath, ThemeCssDirectory);
		if (!Directory.Exists(themeDirectoryPath))
		{
			return [DefaultThemeFileName];
		}

		ImmutableArray<string> themes = Directory
			.EnumerateFiles(themeDirectoryPath, "*.css", SearchOption.TopDirectoryOnly)
			.Select(Path.GetFileName)
			.Where(static fileName => !string.IsNullOrWhiteSpace(fileName))
			.Select(static fileName => fileName!)
			.Where(static fileName => !fileName.StartsWith("theme", StringComparison.OrdinalIgnoreCase))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(static fileName => fileName, StringComparer.OrdinalIgnoreCase)
			.ToImmutableArray();

		if (themes.Length == 0)
		{
			return [DefaultThemeFileName];
		}

		if (themes.Contains(DefaultThemeFileName, StringComparer.OrdinalIgnoreCase))
		{
			return [DefaultThemeFileName, .. themes.Where(theme => !theme.Equals(DefaultThemeFileName, StringComparison.OrdinalIgnoreCase))];
		}

		return themes;
	}

	public static string GetDefaultTheme(IWebHostEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment);

		var availableThemes = GetAvailableThemes(environment);
		return availableThemes.Contains(DefaultThemeFileName, StringComparer.OrdinalIgnoreCase)
			? DefaultThemeFileName
			: availableThemes[0];
	}

	public static bool IsAvailableTheme(IWebHostEnvironment environment, string fileName)
	{
		ArgumentNullException.ThrowIfNull(environment);

		if (string.IsNullOrWhiteSpace(fileName))
		{
			return false;
		}

		return GetAvailableThemes(environment).Contains(fileName.Trim(), StringComparer.OrdinalIgnoreCase);
	}
}