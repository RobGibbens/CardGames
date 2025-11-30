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

    #region Seat Management

    public async Task<GetSeatsResponse> GetSeatsAsync(Guid tableId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/tables/{tableId}/seats");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GetSeatsResponse>();
                return result ?? new GetSeatsResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<GetSeatsResponse>();
            return errorResponse ?? new GetSeatsResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get seats for table {TableId}", tableId);
            return new GetSeatsResponse(false, Error: "Failed to get seats. Please try again.");
        }
    }

    public async Task<SelectSeatResponse> SelectSeatAsync(Guid tableId, int seatNumber, string playerName)
    {
        try
        {
            var request = new SelectSeatRequest(tableId, seatNumber, playerName);
            var response = await _httpClient.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SelectSeatResponse>();
                return result ?? new SelectSeatResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<SelectSeatResponse>();
            return errorResponse ?? new SelectSeatResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select seat {SeatNumber} at table {TableId}", seatNumber, tableId);
            return new SelectSeatResponse(false, Error: "Failed to select seat. Please try again.");
        }
    }

    public async Task<BuyInResponse> BuyInAsync(Guid tableId, int seatNumber, string playerName, int buyInAmount)
    {
        try
        {
            var request = new BuyInRequest(tableId, seatNumber, playerName, buyInAmount);
            var response = await _httpClient.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<BuyInResponse>();
                return result ?? new BuyInResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<BuyInResponse>();
            return errorResponse ?? new BuyInResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to buy in at seat {SeatNumber} at table {TableId}", seatNumber, tableId);
            return new BuyInResponse(false, Error: "Failed to buy in. Please try again.");
        }
    }

    public async Task<SitOutResponse> SitOutAsync(Guid tableId, string playerName, bool sitOut)
    {
        try
        {
            var request = new SitOutRequest(tableId, playerName, sitOut);
            var response = await _httpClient.PostAsJsonAsync($"/api/tables/{tableId}/seats/sit-out", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SitOutResponse>();
                return result ?? new SitOutResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<SitOutResponse>();
            return errorResponse ?? new SitOutResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update sit out status at table {TableId}", tableId);
            return new SitOutResponse(false, Error: "Failed to update sit out status. Please try again.");
        }
    }

    public async Task<SeatChangeResponse> RequestSeatChangeAsync(Guid tableId, string playerName, int desiredSeatNumber)
    {
        try
        {
            var request = new SeatChangeRequest(tableId, playerName, desiredSeatNumber);
            var response = await _httpClient.PostAsJsonAsync($"/api/tables/{tableId}/seats/change", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SeatChangeResponse>();
                return result ?? new SeatChangeResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<SeatChangeResponse>();
            return errorResponse ?? new SeatChangeResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request seat change at table {TableId}", tableId);
            return new SeatChangeResponse(false, Error: "Failed to request seat change. Please try again.");
        }
    }

    public async Task<LeaveTableResponse> StandUpAsync(Guid tableId, string playerName)
    {
        try
        {
            var request = new LeaveTableRequest(tableId, playerName);
            var response = await _httpClient.PostAsJsonAsync($"/api/tables/{tableId}/seats/stand-up", request);

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
            _logger.LogError(ex, "Failed to stand up at table {TableId}", tableId);
            return new LeaveTableResponse(false, Error: "Failed to stand up. Please try again.");
        }
    }

    #endregion
}
