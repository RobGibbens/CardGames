using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class PlayerStatsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PlayerStatsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string Email)> RegisterAndGetTokenAsync()
    {
        var email = $"stats{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(email, "ValidPassword123!", "Stats Test User");
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return (authResult!.Token!, email);
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task GetPlayerStats_WithValidToken_ReturnsStats()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/auth/stats", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PlayerStatsResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.GamesPlayed.Should().Be(0);
        result.GamesWon.Should().Be(0);
        result.GamesLost.Should().Be(0);
        result.TotalChipsWon.Should().Be(0);
        result.TotalChipsLost.Should().Be(0);
    }

    [Fact]
    public async Task GetPlayerStats_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsStats()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/auth/me", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var userInfo = await response.Content.ReadFromJsonAsync<UserInfo>();
        userInfo.Should().NotBeNull();
        userInfo!.GamesPlayed.Should().Be(0);
        userInfo.GamesWon.Should().Be(0);
        userInfo.GamesLost.Should().Be(0);
        userInfo.TotalChipsWon.Should().Be(0);
        userInfo.TotalChipsLost.Should().Be(0);
    }
}
