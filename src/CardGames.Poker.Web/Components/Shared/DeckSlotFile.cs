namespace CardGames.Poker.Web.Components.Shared;

public sealed record DeckSlotFile(string SlotKey, bool IsBack, string? Suit, string? Rank, Microsoft.AspNetCore.Components.ElementReference Input);
