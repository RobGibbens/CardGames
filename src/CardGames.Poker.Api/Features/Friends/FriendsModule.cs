using CardGames.Poker.Api.Features.Auth;
using CardGames.Poker.Shared.Contracts.Friends;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Api.Features.Friends;

public static class FriendsModule
{
    public static IEndpointRouteBuilder MapFriendsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/friends")
            .WithTags("Friends")
            .RequireAuthorization();

        group.MapGet("", GetFriendsListAsync)
            .WithName("GetFriendsList");

        group.MapGet("/invitations/pending", GetPendingInvitationsAsync)
            .WithName("GetPendingInvitations");

        group.MapPost("/invite", SendInvitationAsync)
            .WithName("SendFriendInvitation");

        group.MapPost("/accept/{invitationId}", AcceptInvitationAsync)
            .WithName("AcceptFriendInvitation");

        group.MapPost("/reject/{invitationId}", RejectInvitationAsync)
            .WithName("RejectFriendInvitation");

        group.MapDelete("/{friendId}", RemoveFriendAsync)
            .WithName("RemoveFriend");

        return app;
    }

    private static async Task<IResult> GetFriendsListAsync(
        HttpContext context,
        IFriendsRepository friendsRepository,
        IUserRepository userRepository)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var friendships = await friendsRepository.GetFriendshipsAsync(userId);
        var friends = new List<FriendDto>();

        foreach (var friendship in friendships)
        {
            var friendId = friendship.SenderId == userId ? friendship.ReceiverId : friendship.SenderId;
            var friendUser = await userRepository.GetByIdAsync(friendId);

            if (friendUser is not null)
            {
                friends.Add(new FriendDto(
                    UserId: friendUser.Id,
                    DisplayName: friendUser.DisplayName,
                    Email: friendUser.Email,
                    AvatarUrl: friendUser.AvatarUrl,
                    FriendsSince: friendship.RespondedAt ?? friendship.CreatedAt
                ));
            }
        }

        return Results.Ok(new FriendsListResponse(true, Friends: friends));
    }

    private static async Task<IResult> GetPendingInvitationsAsync(
        HttpContext context,
        IFriendsRepository friendsRepository,
        IUserRepository userRepository)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var receivedInvitations = await friendsRepository.GetPendingInvitationsReceivedAsync(userId);
        var sentInvitations = await friendsRepository.GetPendingInvitationsSentAsync(userId);

        var receivedDtos = await MapInvitationsToDtos(receivedInvitations, userRepository);
        var sentDtos = await MapInvitationsToDtos(sentInvitations, userRepository);

        return Results.Ok(new PendingInvitationsResponse(
            true,
            ReceivedInvitations: receivedDtos,
            SentInvitations: sentDtos
        ));
    }

    private static async Task<IResult> SendInvitationAsync(
        SendFriendInvitationRequest request,
        HttpContext context,
        IFriendsRepository friendsRepository,
        IUserRepository userRepository)
    {
        var senderId = GetUserId(context);
        if (string.IsNullOrEmpty(senderId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.ReceiverEmail))
        {
            return Results.BadRequest(new FriendInvitationResponse(false, Error: "Email is required"));
        }

        var receiver = await userRepository.GetByEmailAsync(request.ReceiverEmail);
        if (receiver is null)
        {
            return Results.NotFound(new FriendInvitationResponse(false, Error: "User not found"));
        }

        if (receiver.Id == senderId)
        {
            return Results.BadRequest(new FriendInvitationResponse(false, Error: "Cannot send friend request to yourself"));
        }

        var existing = await friendsRepository.GetExistingFriendshipAsync(senderId, receiver.Id);
        if (existing is not null)
        {
            if (existing.Status == FriendshipStatus.Accepted)
            {
                return Results.Conflict(new FriendInvitationResponse(false, Error: "Already friends with this user"));
            }
            if (existing.Status == FriendshipStatus.Pending)
            {
                return Results.Conflict(new FriendInvitationResponse(false, Error: "Friend request already pending"));
            }
        }

        var invitation = await friendsRepository.CreateInvitationAsync(senderId, receiver.Id);
        var sender = await userRepository.GetByIdAsync(senderId);

        var dto = new FriendInvitationDto(
            InvitationId: invitation.Id,
            SenderId: invitation.SenderId,
            SenderDisplayName: sender?.DisplayName,
            SenderEmail: sender?.Email,
            SenderAvatarUrl: sender?.AvatarUrl,
            ReceiverId: invitation.ReceiverId,
            ReceiverDisplayName: receiver.DisplayName,
            ReceiverEmail: receiver.Email,
            ReceiverAvatarUrl: receiver.AvatarUrl,
            Status: invitation.Status,
            CreatedAt: invitation.CreatedAt
        );

        return Results.Ok(new FriendInvitationResponse(true, Invitation: dto));
    }

    private static async Task<IResult> AcceptInvitationAsync(
        string invitationId,
        HttpContext context,
        IFriendsRepository friendsRepository)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var accepted = await friendsRepository.AcceptInvitationAsync(invitationId, userId);
        if (accepted is null)
        {
            return Results.NotFound(new FriendOperationResponse(false, Error: "Invitation not found or already processed"));
        }

        return Results.Ok(new FriendOperationResponse(true, Message: "Friend request accepted"));
    }

    private static async Task<IResult> RejectInvitationAsync(
        string invitationId,
        HttpContext context,
        IFriendsRepository friendsRepository)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var rejected = await friendsRepository.RejectInvitationAsync(invitationId, userId);
        if (rejected is null)
        {
            return Results.NotFound(new FriendOperationResponse(false, Error: "Invitation not found or already processed"));
        }

        return Results.Ok(new FriendOperationResponse(true, Message: "Friend request rejected"));
    }

    private static async Task<IResult> RemoveFriendAsync(
        string friendId,
        HttpContext context,
        IFriendsRepository friendsRepository)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var removed = await friendsRepository.RemoveFriendAsync(userId, friendId);
        if (!removed)
        {
            return Results.NotFound(new FriendOperationResponse(false, Error: "Friend not found"));
        }

        return Results.Ok(new FriendOperationResponse(true, Message: "Friend removed"));
    }

    private static string? GetUserId(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
    }

    private static async Task<IReadOnlyList<FriendInvitationDto>> MapInvitationsToDtos(
        IReadOnlyList<FriendshipRecord> invitations,
        IUserRepository userRepository)
    {
        var dtos = new List<FriendInvitationDto>();

        foreach (var invitation in invitations)
        {
            var sender = await userRepository.GetByIdAsync(invitation.SenderId);
            var receiver = await userRepository.GetByIdAsync(invitation.ReceiverId);

            dtos.Add(new FriendInvitationDto(
                InvitationId: invitation.Id,
                SenderId: invitation.SenderId,
                SenderDisplayName: sender?.DisplayName,
                SenderEmail: sender?.Email,
                SenderAvatarUrl: sender?.AvatarUrl,
                ReceiverId: invitation.ReceiverId,
                ReceiverDisplayName: receiver?.DisplayName,
                ReceiverEmail: receiver?.Email,
                ReceiverAvatarUrl: receiver?.AvatarUrl,
                Status: invitation.Status,
                CreatedAt: invitation.CreatedAt
            ));
        }

        return dtos;
    }
}
