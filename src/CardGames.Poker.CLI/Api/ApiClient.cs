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

	public void Dispose()
	{
		_httpClient.Dispose();
	}
}