using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Friends;

public record FriendsListResponse(
    bool Success,
    IReadOnlyList<FriendDto>? Friends = null,
    string? Error = null
);

public record PendingInvitationsResponse(
    bool Success,
    IReadOnlyList<FriendInvitationDto>? ReceivedInvitations = null,
    IReadOnlyList<FriendInvitationDto>? SentInvitations = null,
    string? Error = null
);

public record FriendInvitationResponse(
    bool Success,
    FriendInvitationDto? Invitation = null,
    string? Error = null
);

public record FriendOperationResponse(
    bool Success,
    string? Message = null,
    string? Error = null
);
