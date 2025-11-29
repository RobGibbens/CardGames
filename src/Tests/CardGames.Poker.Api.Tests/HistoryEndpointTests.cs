using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Auth;
using CardGames.Poker.Shared.Contracts.History;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class HistoryEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HistoryEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string Email)> RegisterAndGetTokenAsync()
    {
        var email = $"history{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(email, "ValidPassword123!", "History Test User");
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
    public async Task GetHistory_WithValidToken_ReturnsHistory()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/history", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GameHistoryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Items.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetHistory_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/history");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHistory_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/history?page=1&pageSize=5", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GameHistoryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task GetHistory_WithInvalidPageSize_CorrectsToBounds()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();

        // Act - try with page size > 100, should be capped to 100
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/history?page=1&pageSize=200", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GameHistoryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetGameById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/history/nonexistent-id", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetGameById_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/history/some-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
