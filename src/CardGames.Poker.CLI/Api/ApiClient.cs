using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Poker.CLI.Api;

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// HTTP client for communicating with the CardGames API.
/// </summary>
public class ApiClient : IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly JsonSerializerOptions _jsonOptions;

	public ApiClient(string baseUrl)
	{
		_httpClient = new HttpClient
		{
			BaseAddress = new Uri(baseUrl)
		};

		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonStringEnumConverter() }
		};
	}

	/// <summary>
	/// Creates a new poker game.
	/// </summary>
	public async Task<CreateGameResponse?> CreateGameAsync(CreateGameRequest request)
	{
		var response = await _httpClient.PostAsJsonAsync("/api/v1/games", request, _jsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<CreateGameResponse>(_jsonOptions);
	}

	/// <summary>
	/// Starts a new hand in the game.
	/// </summary>
	public async Task<StartHandResponse?> StartHandAsync(Guid gameId)
	{
		var response = await _httpClient.PostAsync($"/api/v1/games/{gameId}/hands", null);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<StartHandResponse>(_jsonOptions);
	}

	/// <summary>
	/// Gets the current hand state.
	/// </summary>
	public async Task<GetCurrentHandResponse?> GetCurrentHandAsync(Guid gameId)
	{
		return await _httpClient.GetFromJsonAsync<GetCurrentHandResponse>(
			$"/api/v1/games/{gameId}/hands/current",
			_jsonOptions);
	}

	/// <summary>
	/// Gets a player's cards.
	/// </summary>
	public async Task<GetPlayerCardsResponse?> GetPlayerCardsAsync(Guid gameId, Guid playerId)
	{
		return await _httpClient.GetFromJsonAsync<GetPlayerCardsResponse>(
			$"/api/v1/games/{gameId}/players/{playerId}/cards",
			_jsonOptions);
	}

	/// <summary>
	/// Deals cards to all active players.
	/// </summary>
	public async Task<DealCardsResponse?> DealCardsAsync(Guid gameId)
	{
		var response = await _httpClient.PostAsync($"/api/v1/games/{gameId}/hands/current/deal", null);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<DealCardsResponse>(_jsonOptions);
	}

	/// <summary>
	/// Gets available actions for a player.
	/// </summary>
	public async Task<GetAvailableActionsResponse?> GetAvailableActionsAsync(Guid gameId, Guid playerId)
	{
		return await _httpClient.GetFromJsonAsync<GetAvailableActionsResponse>(
			$"/api/v1/games/{gameId}/players/{playerId}/available-actions",
			_jsonOptions);
	}

	/// <summary>
	/// Places a betting action.
	/// </summary>
	public async Task<PlaceActionResponse?> PlaceActionAsync(Guid gameId, PlaceActionRequest request)
	{
		var response = await _httpClient.PostAsJsonAsync(
			$"/api/v1/games/{gameId}/hands/current/actions",
			request,
			_jsonOptions);

		if (!response.IsSuccessStatusCode)
		{
			var error = await response.Content.ReadAsStringAsync();
			return new PlaceActionResponse(false, "", 0, null, false, false, "", error);
		}

		return await response.Content.ReadFromJsonAsync<PlaceActionResponse>(_jsonOptions);
	}


	/// <summary>
	/// Joins an existing game.
	/// </summary>
	public async Task<JoinGameResponse?> JoinGameAsync(Guid gameId, JoinGameRequest request)
	{
		var response = await _httpClient.PostAsJsonAsync($"/api/v1/games/{gameId}/players", request, _jsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<JoinGameResponse>(_jsonOptions);
	}

	/// <summary>
	/// Gets the current game state.
	/// </summary>
	public async Task<GetGameStateResponse?> GetGameStateAsync(Guid gameId)
	{
		return await _httpClient.GetFromJsonAsync<GetGameStateResponse>(
			$"/api/v1/games/{gameId}",
			_jsonOptions);
	}

	/// <summary>
	/// Draws cards (discard and replace) in the draw phase.
	/// </summary>
	public async Task<DrawCardsApiResponse?> DrawCardsAsync(Guid gameId, DrawCardsApiRequest request)
	{
		var response = await _httpClient.PostAsJsonAsync(
			$"/api/v1/games/{gameId}/hands/current/draw",
			request,
			_jsonOptions);

		if (!response.IsSuccessStatusCode)
		{
			var error = await response.Content.ReadAsStringAsync();
			return new DrawCardsApiResponse(false, 0, [], [], false, null, "", error);
		}

		return await response.Content.ReadFromJsonAsync<DrawCardsApiResponse>(_jsonOptions);
	}

	/// <summary>
	/// Performs the showdown and determines winners.
	/// </summary>
	public async Task<ShowdownApiResponse?> ShowdownAsync(Guid gameId)
	{
		var response = await _httpClient.PostAsync($"/api/v1/games/{gameId}/hands/current/showdown", null);

		if (!response.IsSuccessStatusCode)
		{
			var error = await response.Content.ReadAsStringAsync();
			return new ShowdownApiResponse(false, false, [], error);
		}

		return await response.Content.ReadFromJsonAsync<ShowdownApiResponse>(_jsonOptions);
	}

	/// <summary>
	/// Checks if the game can continue.
	/// </summary>
	public async Task<ContinueGameApiResponse?> ContinueGameAsync(Guid gameId)
	{
		return await _httpClient.GetFromJsonAsync<ContinueGameApiResponse>(
			$"/api/v1/games/{gameId}/continue",
			_jsonOptions);
	}

	public void Dispose()
	{
		_httpClient.Dispose();
	}
}