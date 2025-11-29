namespace CardGames.Poker.Web.Utilities;

public static class DisplayHelpers
{
    public static string GetInitials(string? displayName, string? email)
    {
        var name = displayName ?? email ?? "?";
        var parts = name.Split(' ', '@');
        if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[0]) && !string.IsNullOrEmpty(parts[1]))
        {
            return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
        }
        return name[..Math.Min(2, name.Length)].ToUpperInvariant();
    }
}
