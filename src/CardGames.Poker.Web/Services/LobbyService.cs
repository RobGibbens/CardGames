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
}
