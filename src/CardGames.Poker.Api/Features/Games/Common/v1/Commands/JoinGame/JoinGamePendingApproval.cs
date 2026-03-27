namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.JoinGame;

public sealed record JoinGamePendingApproval(Guid JoinRequestId, string HostName);