namespace CardGames.Poker.Shared.Contracts.Lobby;

public record JoinTableRequest(
    Guid TableId,
    string? Password = null);
