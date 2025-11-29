using System.Net.Http.Json;
using System.Security.Claims;
using CardGames.Poker.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace CardGames.Poker.Web.Services;

public interface IAuthStateManager
{
    void SetAuthInfo(string token, string? email, string? displayName);
    void ClearAuthInfo();
    string? GetToken();
    bool IsAuthenticated { get; }
}

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuthStateManager _authStateManager;

    public AuthService(
        IHttpClientFactory httpClientFactory,
        ILogger<AuthService> logger,
        IAuthStateManager authStateManager)
    {
        _httpClient = httpClientFactory.CreateClient("PokerApi");
        _logger = logger;
        _authStateManager = authStateManager;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (result?.Success == true && result.Token is not null)
                {
                    _authStateManager.SetAuthInfo(result.Token, result.Email, result.DisplayName);
                }
                return result ?? new AuthResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            return errorResponse ?? new AuthResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed");
            return new AuthResponse(false, Error: "Registration failed. Please try again.");
        }
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (result?.Success == true && result.Token is not null)
                {
                    _authStateManager.SetAuthInfo(result.Token, result.Email, result.DisplayName);
                }
                return result ?? new AuthResponse(false, Error: "Invalid response from server");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new AuthResponse(false, Error: "Invalid email or password");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            return errorResponse ?? new AuthResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return new AuthResponse(false, Error: "Login failed. Please try again.");
        }
    }

    public void Logout()
    {
        _authStateManager.ClearAuthInfo();
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        try
        {
            var token = _authStateManager.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserInfo>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current user");
        }
        return null;
    }

    public async Task<ProfileResponse> UpdateProfileAsync(UpdateProfileRequest request)
    {
        try
        {
            var token = _authStateManager.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                return new ProfileResponse(false, Error: "Not authenticated");
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/auth/profile");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            httpRequest.Content = JsonContent.Create(request);
            
            var response = await _httpClient.SendAsync(httpRequest);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ProfileResponse>();
                return result ?? new ProfileResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<ProfileResponse>();
            return errorResponse ?? new ProfileResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update profile");
            return new ProfileResponse(false, Error: "Failed to update profile. Please try again.");
        }
    }

    public async Task<PlayerStatsResponse> GetPlayerStatsAsync()
    {
        try
        {
            var token = _authStateManager.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                return new PlayerStatsResponse(false, Error: "Not authenticated");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/stats");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PlayerStatsResponse>();
                return result ?? new PlayerStatsResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<PlayerStatsResponse>();
            return errorResponse ?? new PlayerStatsResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get player stats");
            return new PlayerStatsResponse(false, Error: "Failed to get stats. Please try again.");
        }
    }

    public async Task<AvatarUploadResponse> UploadAvatarAsync(AvatarUploadRequest request)
    {
        try
        {
            var token = _authStateManager.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                return new AvatarUploadResponse(false, Error: "Not authenticated");
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/avatar");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            httpRequest.Content = JsonContent.Create(request);
            
            var response = await _httpClient.SendAsync(httpRequest);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AvatarUploadResponse>();
                return result ?? new AvatarUploadResponse(false, Error: "Invalid response from server");
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<AvatarUploadResponse>();
            return errorResponse ?? new AvatarUploadResponse(false, Error: response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload avatar");
            return new AvatarUploadResponse(false, Error: "Failed to upload avatar. Please try again.");
        }
    }
}

public class AuthStateProvider : AuthenticationStateProvider, IAuthStateManager
{
    private string? _token;
    private string? _email;
    private string? _displayName;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (string.IsNullOrEmpty(_token))
        {
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, _displayName ?? _email ?? "User"),
            new(ClaimTypes.Email, _email ?? string.Empty)
        };

        var identity = new ClaimsIdentity(claims, "jwt");
        var principal = new ClaimsPrincipal(identity);

        return Task.FromResult(new AuthenticationState(principal));
    }

    public void SetAuthInfo(string token, string? email, string? displayName)
    {
        _token = token;
        _email = email;
        _displayName = displayName;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void ClearAuthInfo()
    {
        _token = null;
        _email = null;
        _displayName = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public string? GetToken() => _token;
    
    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
}
