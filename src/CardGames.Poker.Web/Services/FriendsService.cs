using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Friends;

namespace CardGames.Poker.Web.Services;

public class FriendsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FriendsService> _logger;
    private readonly IAuthStateManager _authStateManager;

    public FriendsService(
        IHttpClientFactory httpClientFactory,
        ILogger<FriendsService> logger,
        IAuthStateManager authStateManager)
    {
        _httpClient = httpClientFactory.CreateClient("PokerApi");
        _logger = logger;
        _authStateManager = authStateManager;
    }

    public async Task<FriendsListResponse> GetFriendsListAsync()
    {
        try
        {
            var token = _authStateManager.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                return new FriendsListResponse(false, Error: "Not authenticated");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/friends");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FriendsListResponse>();
                return result ?? new FriendsListResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<FriendsListResponse>();
            return errorResponse ?? new FriendsListResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get friends list");
            return new FriendsListResponse(false, Error: "Failed to get friends list. Please try again.");
        }
    }

    public async Task<PendingInvitationsResponse> GetPendingInvitationsAsync()
    {
        try
        {
            var token = _authStateManager.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                return new PendingInvitationsResponse(false, Error: "Not authenticated");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/friends/invitations/pending");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PendingInvitationsResponse>();
                return result ?? new PendingInvitationsResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<PendingInvitationsResponse>();
            return errorResponse ?? new PendingInvitationsResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending invitations");
            return new PendingInvitationsResponse(false, Error: "Failed to get pending invitations. Please try again.");
        }
    }

    public async Task<FriendInvitationResponse> SendInvitationAsync(string receiverEmail)
    {
        try
        {
            var token = _authStateManager.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                return new FriendInvitationResponse(false, Error: "Not authenticated");
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/friends/invite");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            httpRequest.Content = JsonContent.Create(new SendFriendInvitationRequest(receiverEmail));

            var response = await _httpClient.SendAsync(httpRequest);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FriendInvitationResponse>();
                return result ?? new FriendInvitationResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<FriendInvitationResponse>();
            return errorResponse ?? new FriendInvitationResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send friend invitation");
            return new FriendInvitationResponse(false, Error: "Failed to send invitation. Please try again.");
        }
    }

    public async Task<FriendOperationResponse> AcceptInvitationAsync(string invitationId)
    {
        try
        {
            var token = _authStateManager.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                return new FriendOperationResponse(false, Error: "Not authenticated");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/friends/accept/{invitationId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FriendOperationResponse>();
                return result ?? new FriendOperationResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<FriendOperationResponse>();
            return errorResponse ?? new FriendOperationResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to accept invitation");
            return new FriendOperationResponse(false, Error: "Failed to accept invitation. Please try again.");
        }
    }

    public async Task<FriendOperationResponse> RejectInvitationAsync(string invitationId)
    {
        try
        {
            var token = _authStateManager.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                return new FriendOperationResponse(false, Error: "Not authenticated");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/friends/reject/{invitationId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FriendOperationResponse>();
                return result ?? new FriendOperationResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<FriendOperationResponse>();
            return errorResponse ?? new FriendOperationResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject invitation");
            return new FriendOperationResponse(false, Error: "Failed to reject invitation. Please try again.");
        }
    }

    public async Task<FriendOperationResponse> RemoveFriendAsync(string friendId)
    {
        try
        {
            var token = _authStateManager.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                return new FriendOperationResponse(false, Error: "Not authenticated");
            }

            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/friends/{friendId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FriendOperationResponse>();
                return result ?? new FriendOperationResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<FriendOperationResponse>();
            return errorResponse ?? new FriendOperationResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove friend");
            return new FriendOperationResponse(false, Error: "Failed to remove friend. Please try again.");
        }
    }
}
