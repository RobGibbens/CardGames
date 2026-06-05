namespace CardGames.Poker.Web.Components.Shared;

public enum ThemePreference
{
	Light,
	Dark,
	System
}

public static class ThemePreferenceExtensions
{
	public static ThemePreference Next(this ThemePreference preference)
	{
		return preference switch
		{
			ThemePreference.Light => ThemePreference.Dark,
			ThemePreference.Dark => ThemePreference.System,
			_ => ThemePreference.Light
		};
	}

	public static ThemePreference FromStorageValue(string? value)
	{
		return value?.ToLowerInvariant() switch
		{
			"light" => ThemePreference.Light,
			"dark" => ThemePreference.Dark,
			_ => ThemePreference.System
		};
	}

	public static string ToDisplayName(this ThemePreference preference)
	{
		return preference switch
		{
			ThemePreference.Light => "Light",
			ThemePreference.Dark => "Dark",
			_ => "System"
		};
	}

	public static string ToStorageValue(this ThemePreference preference)
	{
		return preference switch
		{
			ThemePreference.Light => "light",
			ThemePreference.Dark => "dark",
			_ => "system"
		};
	}
}