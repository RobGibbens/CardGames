using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class AvatarUploadTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AvatarUploadTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string Email)> RegisterAndGetTokenAsync()
    {
        var email = $"avatar{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(email, "ValidPassword123!", "Avatar Test User");
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
    public async Task UploadAvatar_WithValidDataUrl_Succeeds()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var avatarDataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var uploadRequest = new AvatarUploadRequest(avatarDataUrl);

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/auth/avatar", token);
        request.Content = JsonContent.Create(uploadRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AvatarUploadResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.AvatarUrl.Should().Be(avatarDataUrl);
    }

    [Fact]
    public async Task UploadAvatar_WithInvalidFormat_ReturnsBadRequest()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var invalidDataUrl = "not-a-data-url";
        var uploadRequest = new AvatarUploadRequest(invalidDataUrl);

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/auth/avatar", token);
        request.Content = JsonContent.Create(uploadRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<AvatarUploadResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid image format");
    }

    [Fact]
    public async Task UploadAvatar_WithEmptyData_ReturnsBadRequest()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var uploadRequest = new AvatarUploadRequest("");

        // Act
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/auth/avatar", token);
        request.Content = JsonContent.Create(uploadRequest);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<AvatarUploadResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("required");
    }

    [Fact]
    public async Task UploadAvatar_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var uploadRequest = new AvatarUploadRequest("data:image/png;base64,ABC123");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/avatar", uploadRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UploadAvatar_UpdatesUserProfile()
    {
        // Arrange
        var (token, _) = await RegisterAndGetTokenAsync();
        var avatarDataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var uploadRequest = new AvatarUploadRequest(avatarDataUrl);

        // Upload avatar
        using var uploadReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/auth/avatar", token);
        uploadReq.Content = JsonContent.Create(uploadRequest);
        await _client.SendAsync(uploadReq);

        // Act - Get current user
        using var getRequest = CreateAuthenticatedRequest(HttpMethod.Get, "/api/auth/me", token);
        var response = await _client.SendAsync(getRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var userInfo = await response.Content.ReadFromJsonAsync<UserInfo>();
        userInfo.Should().NotBeNull();
        userInfo!.AvatarUrl.Should().Be(avatarDataUrl);
    }
}
