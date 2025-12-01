namespace CardGames.Poker.Shared.Enums;

/// <summary>
/// Specifies the type of chat message.
/// </summary>
public enum ChatMessageType
{
    /// <summary>A regular player chat message.</summary>
    Player,

    /// <summary>A system announcement message (e.g., player joins, wins pot).</summary>
    System,

    /// <summary>A dealer message (game event announcements).</summary>
    Dealer
}
