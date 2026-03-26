namespace CardGames.Poker.Web.Components.Shared;

public enum ThemePreference
{
    Light,
    Dark,
    System
}

public static class ThemePreferenceExtensions
{
    public static ThemePreference Next(this ThemePreference preference) => preference switch
    {
        ThemePreference.Light => ThemePreference.Dark,
        ThemePreference.Dark => ThemePreference.System,
        _ => ThemePreference.Light
    };

    public static ThemePreference FromStorageValue(string? value) => value?.ToLowerInvariant() switch
    {
        "light" => ThemePreference.Light,
        "dark" => ThemePreference.Dark,
        "system" => ThemePreference.System,
        _ => ThemePreference.System
    };

    public static string ToDisplayName(this ThemePreference preference) => preference switch
    {
        ThemePreference.Light => "Light",
        ThemePreference.Dark => "Dark",
        _ => "System"
    };

    public static string ToStorageValue(this ThemePreference preference) => preference switch
    {
        ThemePreference.Light => "light",
        ThemePreference.Dark => "dark",
        _ => "system"
    };
}