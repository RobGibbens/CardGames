using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Chat;
using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Service for interacting with the Chat API endpoints.
/// </summary>
public class ChatApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChatApiService> _logger;

    public ChatApiService(
        IHttpClientFactory httpClientFactory,
        ILogger<ChatApiService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("PokerApi");
        _logger = logger;
    }

    /// <summary>
    /// Sends a chat message to a table.
    /// </summary>
    public async Task<SendChatMessageResponse> SendMessageAsync(Guid tableId, string playerName, string content)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/chat/{tableId}/messages");
            request.Headers.Add("X-Player-Name", playerName);
            request.Content = JsonContent.Create(new SendChatMessageRequest(tableId, content));

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SendChatMessageResponse>();
                return result ?? new SendChatMessageResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<SendChatMessageResponse>();
            return errorResponse ?? new SendChatMessageResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send chat message");
            return new SendChatMessageResponse(false, Error: "Failed to send message. Please try again.");
        }
    }

    /// <summary>
    /// Gets chat history for a table.
    /// </summary>
    public async Task<GetChatHistoryResponse> GetChatHistoryAsync(Guid tableId, int maxMessages = 50)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/chat/{tableId}/messages?maxMessages={maxMessages}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GetChatHistoryResponse>();
                return result ?? new GetChatHistoryResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<GetChatHistoryResponse>();
            return errorResponse ?? new GetChatHistoryResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chat history");
            return new GetChatHistoryResponse(false, Error: "Failed to load chat history.");
        }
    }

    /// <summary>
    /// Mutes a player at a table.
    /// </summary>
    public async Task<MutePlayerResponse> MutePlayerAsync(Guid tableId, string playerName, string playerToMute)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/chat/{tableId}/mute");
            request.Headers.Add("X-Player-Name", playerName);
            request.Content = JsonContent.Create(new MutePlayerRequest(tableId, playerToMute));

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<MutePlayerResponse>();
                return result ?? new MutePlayerResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<MutePlayerResponse>();
            return errorResponse ?? new MutePlayerResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mute player");
            return new MutePlayerResponse(false, Error: "Failed to mute player. Please try again.");
        }
    }

    /// <summary>
    /// Unmutes a player at a table.
    /// </summary>
    public async Task<UnmutePlayerResponse> UnmutePlayerAsync(Guid tableId, string playerName, string playerToUnmute)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/chat/{tableId}/unmute");
            request.Headers.Add("X-Player-Name", playerName);
            request.Content = JsonContent.Create(new UnmutePlayerRequest(tableId, playerToUnmute));

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UnmutePlayerResponse>();
                return result ?? new UnmutePlayerResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<UnmutePlayerResponse>();
            return errorResponse ?? new UnmutePlayerResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unmute player");
            return new UnmutePlayerResponse(false, Error: "Failed to unmute player. Please try again.");
        }
    }

    /// <summary>
    /// Gets the list of muted players for a table.
    /// </summary>
    public async Task<GetMutedPlayersResponse> GetMutedPlayersAsync(Guid tableId, string playerName)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/chat/{tableId}/muted");
            request.Headers.Add("X-Player-Name", playerName);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GetMutedPlayersResponse>();
                return result ?? new GetMutedPlayersResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<GetMutedPlayersResponse>();
            return errorResponse ?? new GetMutedPlayersResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get muted players");
            return new GetMutedPlayersResponse(false, Error: "Failed to get muted players.");
        }
    }

    /// <summary>
    /// Gets the table chat status.
    /// </summary>
    public async Task<SetTableChatStatusResponse> GetChatStatusAsync(Guid tableId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/chat/{tableId}/status");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SetTableChatStatusResponse>();
                return result ?? new SetTableChatStatusResponse(false, false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<SetTableChatStatusResponse>();
            return errorResponse ?? new SetTableChatStatusResponse(false, false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chat status");
            return new SetTableChatStatusResponse(false, false, Error: "Failed to get chat status.");
        }
    }
}
