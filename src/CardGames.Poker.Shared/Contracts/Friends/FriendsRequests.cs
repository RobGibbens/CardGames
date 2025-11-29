namespace CardGames.Poker.Shared.Contracts.Friends;

public record SendFriendInvitationRequest(string ReceiverEmail);

public record RespondToInvitationRequest(string InvitationId);
