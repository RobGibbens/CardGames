namespace CardGames.Poker.Shared.Enums;

/// <summary>
/// Represents the privacy setting of a poker table.
/// </summary>
public enum TablePrivacy
{
    /// <summary>Table is visible and joinable by anyone.</summary>
    Public,

    /// <summary>Table is only joinable via direct link.</summary>
    Private,

    /// <summary>Table requires a password to join.</summary>
    Password
}
