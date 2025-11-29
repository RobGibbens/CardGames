using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class ChipBalanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private const long DefaultInitialChipBalance = 1000;

    public ChipBalanceTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string Email)> RegisterAndGetTokenAsync()
    {
        var email = $"chip{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(email, "ValidPassword123!", "Chip Test User");
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
    public async Task Register_SetsInitialChipBalance()
    {
        // Arrange
        var email = $"newuser{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest(email, "ValidPassword123!", "New User");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        result.Should().NotBeNull();
        result!.ChipBalance.Should().Be(DefaultInitialChipBalance);
    }

    [Fact]
    public async Task Login_ReturnsChipBalance()
    {
        // Arrange
        var email = $"loginchips{Guid.NewGuid():N}@example.com";
        var password = "ValidPassword123!";
        var registerRequest = new RegisterRequest(email, password, "Login Chips User");
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act
        var loginRequest = new LoginRequest(email, password);
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        result.Should().NotBeNull();
        result!.ChipBalance.Should().Be(DefaultInitialChipBalance);
    }

    [Fact]
    public async Task GetChipBalance_WithValidToken_ReturnsBalance()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/auth/chips", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ChipBalanceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Balance.Should().Be(DefaultInitialChipBalance);
    }

    [Fact]
    public async Task GetChipBalance_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/chips");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdjustChipBalance_PositiveAmount_IncreasesBalance()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var adjustRequest = new UpdateChipBalanceRequest(500, "Won pot");

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/auth/chips/adjust", token);
        request.Content = JsonContent.Create(adjustRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ChipBalanceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Balance.Should().Be(DefaultInitialChipBalance + 500);
    }

    [Fact]
    public async Task AdjustChipBalance_NegativeAmount_DecreasesBalance()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var adjustRequest = new UpdateChipBalanceRequest(-300, "Lost pot");

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/auth/chips/adjust", token);
        request.Content = JsonContent.Create(adjustRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ChipBalanceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Balance.Should().Be(DefaultInitialChipBalance - 300);
    }

    [Fact]
    public async Task AdjustChipBalance_InsufficientBalance_ReturnsBadRequest()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var adjustRequest = new UpdateChipBalanceRequest(-5000, "Attempt to go negative");

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/auth/chips/adjust", token);
        request.Content = JsonContent.Create(adjustRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ChipBalanceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Insufficient");
    }

    [Fact]
    public async Task AdjustChipBalance_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var adjustRequest = new UpdateChipBalanceRequest(100);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/chips/adjust", adjustRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetChipBalance_ValidBalance_SetsBalance()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var setRequest = new SetChipBalanceRequest(5000, "Admin grant");

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Put, "/api/auth/chips", token);
        request.Content = JsonContent.Create(setRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ChipBalanceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Balance.Should().Be(5000);
    }

    [Fact]
    public async Task SetChipBalance_ZeroBalance_SetsBalanceToZero()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var setRequest = new SetChipBalanceRequest(0, "Reset balance");

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Put, "/api/auth/chips", token);
        request.Content = JsonContent.Create(setRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ChipBalanceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Balance.Should().Be(0);
    }

    [Fact]
    public async Task SetChipBalance_NegativeBalance_ReturnsBadRequest()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var setRequest = new SetChipBalanceRequest(-100, "Invalid negative");

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Put, "/api/auth/chips", token);
        request.Content = JsonContent.Create(setRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ChipBalanceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("negative");
    }

    [Fact]
    public async Task SetChipBalance_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var setRequest = new SetChipBalanceRequest(100);

        // Act
        var response = await _client.PutAsJsonAsync("/api/auth/chips", setRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsChipBalance()
    {
        // Arrange
        var (token, email) = await RegisterAndGetTokenAsync();

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/auth/me", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var userInfo = await response.Content.ReadFromJsonAsync<UserInfo>();
        userInfo.Should().NotBeNull();
        userInfo!.ChipBalance.Should().Be(DefaultInitialChipBalance);
    }

    [Fact]
    public async Task ChipBalance_MultipleOperations_AccumulatesCorrectly()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();

        // Act - Series of operations
        // Start: 1000
        // +500 = 1500
        using var request1 = CreateAuthenticatedRequest(HttpMethod.Post, "/api/auth/chips/adjust", token);
        request1.Content = JsonContent.Create(new UpdateChipBalanceRequest(500));
        await _client.SendAsync(request1);

        // -200 = 1300
        using var request2 = CreateAuthenticatedRequest(HttpMethod.Post, "/api/auth/chips/adjust", token);
        request2.Content = JsonContent.Create(new UpdateChipBalanceRequest(-200));
        await _client.SendAsync(request2);

        // +1000 = 2300
        using var request3 = CreateAuthenticatedRequest(HttpMethod.Post, "/api/auth/chips/adjust", token);
        request3.Content = JsonContent.Create(new UpdateChipBalanceRequest(1000));
        await _client.SendAsync(request3);

        // Get final balance
        using var getRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/auth/chips", token);
        var response = await _client.SendAsync(getRequest);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ChipBalanceResponse>();
        result.Should().NotBeNull();
        result!.Balance.Should().Be(2300);
    }
}
