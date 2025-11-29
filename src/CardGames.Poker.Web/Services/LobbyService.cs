using System.Net.Http.Json;
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
        bool? hideFullTables = null,
        bool? hideEmptyTables = null)
    {
        try
        {
            var queryParams = new List<string>();

            if (variant.HasValue)
            {
                queryParams.Add($"variant={variant.Value}");
            }
            if (minSmallBlind.HasValue)
            {
                queryParams.Add($"minSmallBlind={minSmallBlind.Value}");
            }
            if (maxSmallBlind.HasValue)
            {
                queryParams.Add($"maxSmallBlind={maxSmallBlind.Value}");
            }
            if (minAvailableSeats.HasValue)
            {
                queryParams.Add($"minAvailableSeats={minAvailableSeats.Value}");
            }
            if (hideFullTables.HasValue)
            {
                queryParams.Add($"hideFullTables={hideFullTables.Value}");
            }
            if (hideEmptyTables.HasValue)
            {
                queryParams.Add($"hideEmptyTables={hideEmptyTables.Value}");
            }

            var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            var response = await _httpClient.GetAsync($"/api/tables{queryString}");

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
}
