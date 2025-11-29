using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Auth;
using CardGames.Poker.Shared.Contracts.Friends;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class FriendsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public FriendsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string Email, string UserId)> RegisterAndGetTokenAsync(string? displayName = null)
    {
        var email = $"friend{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(email, "ValidPassword123!", displayName ?? "Friend Test User");
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult!.Token);
        var meResponse = await _client.SendAsync(meRequest);
        var userInfo = await meResponse.Content.ReadFromJsonAsync<UserInfo>();
        
        return (authResult.Token!, email, userInfo!.Id);
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task GetFriendsList_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        var (token, _, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/friends", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FriendsListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Friends.Should().NotBeNull();
        result.Friends.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFriendsList_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/friends");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPendingInvitations_WhenEmpty_ReturnsEmptyLists()
    {
        // Arrange
        var (token, _, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/friends/invitations/pending", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PendingInvitationsResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.ReceivedInvitations.Should().NotBeNull();
        result.ReceivedInvitations.Should().BeEmpty();
        result.SentInvitations.Should().NotBeNull();
        result.SentInvitations.Should().BeEmpty();
    }

    [Fact]
    public async Task SendInvitation_ToExistingUser_ReturnsSuccess()
    {
        // Arrange
        var (senderToken, _, _) = await RegisterAndGetTokenAsync("Sender");
        var (_, receiverEmail, _) = await RegisterAndGetTokenAsync("Receiver");

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", senderToken);
        request.Content = JsonContent.Create(new SendFriendInvitationRequest(receiverEmail));
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FriendInvitationResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Invitation.Should().NotBeNull();
        result.Invitation!.SenderDisplayName.Should().Be("Sender");
        result.Invitation.ReceiverEmail.Should().Be(receiverEmail);
    }

    [Fact]
    public async Task SendInvitation_ToNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var (token, _, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", token);
        request.Content = JsonContent.Create(new SendFriendInvitationRequest("nonexistent@example.com"));
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<FriendInvitationResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task SendInvitation_ToSelf_ReturnsBadRequest()
    {
        // Arrange
        var (token, email, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", token);
        request.Content = JsonContent.Create(new SendFriendInvitationRequest(email));
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<FriendInvitationResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("yourself");
    }

    [Fact]
    public async Task SendInvitation_WithEmptyEmail_ReturnsBadRequest()
    {
        // Arrange
        var (token, _, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", token);
        request.Content = JsonContent.Create(new SendFriendInvitationRequest(""));
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<FriendInvitationResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SendInvitation_DuplicateRequest_ReturnsConflict()
    {
        // Arrange
        var (senderToken, _, _) = await RegisterAndGetTokenAsync("Sender");
        var (_, receiverEmail, _) = await RegisterAndGetTokenAsync("Receiver");

        // Send first invitation
        using var firstRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", senderToken);
        firstRequest.Content = JsonContent.Create(new SendFriendInvitationRequest(receiverEmail));
        await _client.SendAsync(firstRequest);

        // Act - send duplicate invitation
        using var duplicateRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", senderToken);
        duplicateRequest.Content = JsonContent.Create(new SendFriendInvitationRequest(receiverEmail));
        var response = await _client.SendAsync(duplicateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var result = await response.Content.ReadFromJsonAsync<FriendInvitationResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task AcceptInvitation_ValidInvitation_ReturnsSuccess()
    {
        // Arrange
        var (senderToken, _, _) = await RegisterAndGetTokenAsync("Sender");
        var (receiverToken, receiverEmail, _) = await RegisterAndGetTokenAsync("Receiver");

        // Send invitation
        using var inviteRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", senderToken);
        inviteRequest.Content = JsonContent.Create(new SendFriendInvitationRequest(receiverEmail));
        var inviteResponse = await _client.SendAsync(inviteRequest);
        var invitation = await inviteResponse.Content.ReadFromJsonAsync<FriendInvitationResponse>();

        // Act
        using var acceptRequest = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/friends/accept/{invitation!.Invitation!.InvitationId}", receiverToken);
        var response = await _client.SendAsync(acceptRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FriendOperationResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().Contain("accepted");
    }

    [Fact]
    public async Task AcceptInvitation_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var (token, _, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/accept/nonexistent", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RejectInvitation_ValidInvitation_ReturnsSuccess()
    {
        // Arrange
        var (senderToken, _, _) = await RegisterAndGetTokenAsync("Sender");
        var (receiverToken, receiverEmail, _) = await RegisterAndGetTokenAsync("Receiver");

        // Send invitation
        using var inviteRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", senderToken);
        inviteRequest.Content = JsonContent.Create(new SendFriendInvitationRequest(receiverEmail));
        var inviteResponse = await _client.SendAsync(inviteRequest);
        var invitation = await inviteResponse.Content.ReadFromJsonAsync<FriendInvitationResponse>();

        // Act
        using var rejectRequest = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/friends/reject/{invitation!.Invitation!.InvitationId}", receiverToken);
        var response = await _client.SendAsync(rejectRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FriendOperationResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().Contain("rejected");
    }

    [Fact]
    public async Task GetFriendsList_AfterAcceptingInvitation_ReturnsFriend()
    {
        // Arrange
        var (senderToken, _, _) = await RegisterAndGetTokenAsync("Sender");
        var (receiverToken, receiverEmail, _) = await RegisterAndGetTokenAsync("Receiver");

        // Send and accept invitation
        using var inviteRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", senderToken);
        inviteRequest.Content = JsonContent.Create(new SendFriendInvitationRequest(receiverEmail));
        var inviteResponse = await _client.SendAsync(inviteRequest);
        var invitation = await inviteResponse.Content.ReadFromJsonAsync<FriendInvitationResponse>();

        using var acceptRequest = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/friends/accept/{invitation!.Invitation!.InvitationId}", receiverToken);
        await _client.SendAsync(acceptRequest);

        // Act - get sender's friends
        using var friendsRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/friends", senderToken);
        var response = await _client.SendAsync(friendsRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FriendsListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Friends.Should().HaveCount(1);
        result.Friends![0].DisplayName.Should().Be("Receiver");
    }

    [Fact]
    public async Task RemoveFriend_ExistingFriend_ReturnsSuccess()
    {
        // Arrange
        var (senderToken, _, _) = await RegisterAndGetTokenAsync("Sender");
        var (receiverToken, receiverEmail, receiverId) = await RegisterAndGetTokenAsync("Receiver");

        // Send and accept invitation
        using var inviteRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", senderToken);
        inviteRequest.Content = JsonContent.Create(new SendFriendInvitationRequest(receiverEmail));
        var inviteResponse = await _client.SendAsync(inviteRequest);
        var invitation = await inviteResponse.Content.ReadFromJsonAsync<FriendInvitationResponse>();

        using var acceptRequest = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/friends/accept/{invitation!.Invitation!.InvitationId}", receiverToken);
        await _client.SendAsync(acceptRequest);

        // Act
        using var removeRequest = CreateAuthenticatedRequest(HttpMethod.Delete, $"/api/friends/{receiverId}", senderToken);
        var response = await _client.SendAsync(removeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FriendOperationResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveFriend_NonExistentFriend_ReturnsNotFound()
    {
        // Arrange
        var (token, _, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Delete, "/api/friends/nonexistent", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPendingInvitations_ShowsReceivedInvitation()
    {
        // Arrange
        var (senderToken, _, _) = await RegisterAndGetTokenAsync("Sender");
        var (receiverToken, receiverEmail, _) = await RegisterAndGetTokenAsync("Receiver");

        // Send invitation
        using var inviteRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", senderToken);
        inviteRequest.Content = JsonContent.Create(new SendFriendInvitationRequest(receiverEmail));
        await _client.SendAsync(inviteRequest);

        // Act
        using var pendingRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/friends/invitations/pending", receiverToken);
        var response = await _client.SendAsync(pendingRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PendingInvitationsResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.ReceivedInvitations.Should().HaveCount(1);
        result.ReceivedInvitations![0].SenderDisplayName.Should().Be("Sender");
    }

    [Fact]
    public async Task GetPendingInvitations_ShowsSentInvitation()
    {
        // Arrange
        var (senderToken, _, _) = await RegisterAndGetTokenAsync("Sender");
        var (_, receiverEmail, _) = await RegisterAndGetTokenAsync("Receiver");

        // Send invitation
        using var inviteRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/api/friends/invite", senderToken);
        inviteRequest.Content = JsonContent.Create(new SendFriendInvitationRequest(receiverEmail));
        await _client.SendAsync(inviteRequest);

        // Act
        using var pendingRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/friends/invitations/pending", senderToken);
        var response = await _client.SendAsync(pendingRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PendingInvitationsResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.SentInvitations.Should().HaveCount(1);
        result.SentInvitations![0].ReceiverDisplayName.Should().Be("Receiver");
    }
}
