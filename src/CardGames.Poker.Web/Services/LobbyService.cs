using System.Net.Http.Json;
using System.Web;
using CardGames.Poker.Shared.Contracts.Lobby;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Web.Services;

public class LobbyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LobbyService> _logger;

    public LobbyService(
        IHttpClientFactory httpClientFactory,
        ILogger<LobbyService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("PokerApi");
        _logger = logger;
    }

    public async Task<TablesListResponse> GetTablesAsync(
        PokerVariant? variant = null,
        int? minSmallBlind = null,
        int? maxSmallBlind = null,
        int? minAvailableSeats = null,
        bool hideFullTables = false,
        bool hideEmptyTables = false)
    {
        try
        {
            var query = HttpUtility.ParseQueryString(string.Empty);

            if (variant.HasValue)
            {
                query["variant"] = variant.Value.ToString();
            }
            if (minSmallBlind.HasValue)
            {
                query["minSmallBlind"] = minSmallBlind.Value.ToString();
            }
            if (maxSmallBlind.HasValue)
            {
                query["maxSmallBlind"] = maxSmallBlind.Value.ToString();
            }
            if (minAvailableSeats.HasValue)
            {
                query["minAvailableSeats"] = minAvailableSeats.Value.ToString();
            }
            if (hideFullTables)
            {
                query["hideFullTables"] = "true";
            }
            if (hideEmptyTables)
            {
                query["hideEmptyTables"] = "true";
            }

            var queryString = query.ToString();
            var url = string.IsNullOrEmpty(queryString) ? "/api/tables" : $"/api/tables?{queryString}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
                return result ?? new TablesListResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<TablesListResponse>();
            return errorResponse ?? new TablesListResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tables list");
            return new TablesListResponse(false, Error: "Failed to get tables list. Please try again.");
        }
    }

    public async Task<CreateTableResponse> CreateTableAsync(CreateTableRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/tables", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
                return result ?? new CreateTableResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
            return errorResponse ?? new CreateTableResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create table");
            return new CreateTableResponse(false, Error: "Failed to create table. Please try again.");
        }
    }

    public async Task<JoinTableResponse> JoinTableAsync(Guid tableId, string? password = null)
    {
        try
        {
            var request = new JoinTableRequest(tableId, password);
            var response = await _httpClient.PostAsJsonAsync($"/api/tables/{tableId}/join", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JoinTableResponse>();
                return result ?? new JoinTableResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<JoinTableResponse>();
            return errorResponse ?? new JoinTableResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join table {TableId}", tableId);
            return new JoinTableResponse(false, Error: "Failed to join table. Please try again.");
        }
    }

    public async Task<QuickJoinResponse> QuickJoinAsync(
        PokerVariant? variant = null,
        int? minSmallBlind = null,
        int? maxSmallBlind = null)
    {
        try
        {
            var request = new QuickJoinRequest(variant, minSmallBlind, maxSmallBlind);
            var response = await _httpClient.PostAsJsonAsync("/api/tables/quick-join", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<QuickJoinResponse>();
                return result ?? new QuickJoinResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<QuickJoinResponse>();
            return errorResponse ?? new QuickJoinResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to quick join");
            return new QuickJoinResponse(false, Error: "Failed to find a table. Please try again.");
        }
    }

    public async Task<JoinWaitingListResponse> JoinWaitingListAsync(Guid tableId, string playerName)
    {
        try
        {
            var request = new JoinWaitingListRequest(tableId, playerName);
            var response = await _httpClient.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JoinWaitingListResponse>();
                return result ?? new JoinWaitingListResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<JoinWaitingListResponse>();
            return errorResponse ?? new JoinWaitingListResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join waiting list for table {TableId}", tableId);
            return new JoinWaitingListResponse(false, Error: "Failed to join waiting list. Please try again.");
        }
    }

    public async Task<LeaveWaitingListResponse> LeaveWaitingListAsync(Guid tableId, string playerName)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/tables/{tableId}/waiting-list/{Uri.EscapeDataString(playerName)}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LeaveWaitingListResponse>();
                return result ?? new LeaveWaitingListResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<LeaveWaitingListResponse>();
            return errorResponse ?? new LeaveWaitingListResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave waiting list for table {TableId}", tableId);
            return new LeaveWaitingListResponse(false, Error: "Failed to leave waiting list. Please try again.");
        }
    }

    public async Task<GetWaitingListResponse> GetWaitingListAsync(Guid tableId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/tables/{tableId}/waiting-list");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GetWaitingListResponse>();
                return result ?? new GetWaitingListResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<GetWaitingListResponse>();
            return errorResponse ?? new GetWaitingListResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get waiting list for table {TableId}", tableId);
            return new GetWaitingListResponse(false, Error: "Failed to get waiting list. Please try again.");
        }
    }

    public async Task<LeaveTableResponse> LeaveTableAsync(Guid tableId, string playerName)
    {
        try
        {
            var request = new LeaveTableRequest(tableId, playerName);
            var response = await _httpClient.PostAsJsonAsync($"/api/tables/{tableId}/leave", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LeaveTableResponse>();
                return result ?? new LeaveTableResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<LeaveTableResponse>();
            return errorResponse ?? new LeaveTableResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave table {TableId}", tableId);
            return new LeaveTableResponse(false, Error: "Failed to leave table. Please try again.");
        }
    }
}
