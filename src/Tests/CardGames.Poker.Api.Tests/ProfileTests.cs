using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class ProfileTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProfileTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string Email)> RegisterAndGetTokenAsync(string? displayName = null)
    {
        var email = $"profile{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(email, "ValidPassword123!", displayName ?? "Profile Test User");
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
    public async Task UpdateProfile_DisplayName_UpdatesSuccessfully()
    {
        // Arrange
        var (token, email) = await RegisterAndGetTokenAsync("Original Name");
        var updateRequest = new UpdateProfileRequest(DisplayName: "Updated Name");

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Put, "/api/auth/profile", token);
        request.Content = JsonContent.Create(updateRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.DisplayName.Should().Be("Updated Name");
        result.Email.Should().Be(email);
    }

    [Fact]
    public async Task UpdateProfile_AvatarUrl_UpdatesSuccessfully()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var updateRequest = new UpdateProfileRequest(AvatarUrl: "https://example.com/avatar.png");

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Put, "/api/auth/profile", token);
        request.Content = JsonContent.Create(updateRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.AvatarUrl.Should().Be("https://example.com/avatar.png");
    }

    [Fact]
    public async Task UpdateProfile_BothFields_UpdatesSuccessfully()
    {
        // Arrange
        var (token, email) = await RegisterAndGetTokenAsync("Original Name");
        var updateRequest = new UpdateProfileRequest(
            DisplayName: "New Display Name",
            AvatarUrl: "https://example.com/new-avatar.png"
        );

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Put, "/api/auth/profile", token);
        request.Content = JsonContent.Create(updateRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.DisplayName.Should().Be("New Display Name");
        result.AvatarUrl.Should().Be("https://example.com/new-avatar.png");
    }

    [Fact]
    public async Task UpdateProfile_PreservesExistingValues_WhenNullPassed()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync("Original Name");
        
        // First update - set avatar
        using var firstRequest = CreateAuthenticatedRequest(HttpMethod.Put, "/api/auth/profile", token);
        firstRequest.Content = JsonContent.Create(new UpdateProfileRequest(AvatarUrl: "https://example.com/avatar.png"));
        await _client.SendAsync(firstRequest);

        // Second update - only update display name (avatar should be preserved)
        using var secondRequest = CreateAuthenticatedRequest(HttpMethod.Put, "/api/auth/profile", token);
        secondRequest.Content = JsonContent.Create(new UpdateProfileRequest(DisplayName: "New Name"));
        var response = await _client.SendAsync(secondRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("New Name");
        result.AvatarUrl.Should().Be("https://example.com/avatar.png");
    }

    [Fact]
    public async Task UpdateProfile_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var updateRequest = new UpdateProfileRequest(DisplayName: "New Name");

        // Act
        var response = await _client.PutAsJsonAsync("/api/auth/profile", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsChipBalance()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var updateRequest = new UpdateProfileRequest(DisplayName: "Updated Name");

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Put, "/api/auth/profile", token);
        request.Content = JsonContent.Create(updateRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        result.Should().NotBeNull();
        result!.ChipBalance.Should().Be(1000); // Default initial balance
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsAvatarUrl()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        
        // Set avatar
        using var updateRequest = CreateAuthenticatedRequest(HttpMethod.Put, "/api/auth/profile", token);
        updateRequest.Content = JsonContent.Create(new UpdateProfileRequest(AvatarUrl: "https://example.com/avatar.png"));
        await _client.SendAsync(updateRequest);

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/auth/me", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var userInfo = await response.Content.ReadFromJsonAsync<UserInfo>();
        userInfo.Should().NotBeNull();
        userInfo!.AvatarUrl.Should().Be("https://example.com/avatar.png");
    }

    [Fact]
    public async Task Login_ReturnsAvatarUrl()
    {
        // Arrange
        var email = $"avatar{Guid.NewGuid():N}@example.com";
        var password = "ValidPassword123!";
        var registerRequest = new RegisterRequest(email, password, "Avatar User");
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        
        // Set avatar
        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, "/api/auth/profile");
        updateRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult!.Token);
        updateRequest.Content = JsonContent.Create(new UpdateProfileRequest(AvatarUrl: "https://example.com/user-avatar.png"));
        await _client.SendAsync(updateRequest);

        // Act
        var loginRequest = new LoginRequest(email, password);
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        result.Should().NotBeNull();
        result!.AvatarUrl.Should().Be("https://example.com/user-avatar.png");
    }
}
