using System.Net.Http.Json;
using System.Security.Claims;
using CardGames.Poker.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace CardGames.Poker.Web.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthStateProvider _authStateProvider;

    public AuthService(
        IHttpClientFactory httpClientFactory,
        ILogger<AuthService> logger,
        AuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClientFactory.CreateClient("PokerApi");
        _logger = logger;
        _authStateProvider = (AuthStateProvider)authStateProvider;
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
                    _authStateProvider.SetAuthInfo(result.Token, result.Email, result.DisplayName);
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
                    _authStateProvider.SetAuthInfo(result.Token, result.Email, result.DisplayName);
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
        _authStateProvider.ClearAuthInfo();
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        try
        {
            var token = _authStateProvider.GetToken();
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
}

public class AuthStateProvider : AuthenticationStateProvider
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
