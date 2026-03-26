using CardGames.Contracts.SignalR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using System.Security.Claims;
using System.Web;

namespace CardGames.Poker.Web.Services;

public sealed class NotificationHubClient : IAsyncDisposable
{
	private readonly IConfiguration _configuration;
	private readonly AuthenticationStateProvider _authStateProvider;
	private readonly ILogger<NotificationHubClient> _logger;
	private HubConnection? _hubConnection;

	public event Func<GameJoinRequestReceivedDto, Task>? OnGameJoinRequestReceived;
	public event Func<GameJoinRequestResolvedDto, Task>? OnGameJoinRequestResolved;

	public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

	public NotificationHubClient(
		IConfiguration configuration,
		AuthenticationStateProvider authStateProvider,
		ILogger<NotificationHubClient> logger)
	{
		_configuration = configuration;
		_authStateProvider = authStateProvider;
		_logger = logger;
	}

	public async Task ConnectAsync(CancellationToken cancellationToken = default)
	{
		if (_hubConnection is not null && _hubConnection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
		{
			return;
		}

		var userInfo = await GetUserInfoAsync();
		var hubUrl = GetHubUrl(userInfo);

		_hubConnection = new HubConnectionBuilder()
			.WithUrl(hubUrl, options =>
			{
				if (userInfo is not null)
				{
					options.Headers["X-User-Authenticated"] = "true";
					if (!string.IsNullOrEmpty(userInfo.Value.UserId))
					{
						options.Headers["X-User-Id"] = userInfo.Value.UserId;
					}
					if (!string.IsNullOrEmpty(userInfo.Value.UserName))
					{
						options.Headers["X-User-Name"] = userInfo.Value.UserName;
					}
					if (!string.IsNullOrEmpty(userInfo.Value.UserEmail))
					{
						options.Headers["X-User-Email"] = userInfo.Value.UserEmail;
					}
				}
			})
			.WithAutomaticReconnect(new[]
			{
				TimeSpan.Zero,
				TimeSpan.FromSeconds(2),
				TimeSpan.FromSeconds(5),
				TimeSpan.FromSeconds(10),
				TimeSpan.FromSeconds(30)
			})
			.Build();

		_hubConnection.On<GameJoinRequestReceivedDto>("GameJoinRequestReceived", HandleGameJoinRequestReceivedAsync);
		_hubConnection.On<GameJoinRequestResolvedDto>("GameJoinRequestResolved", HandleGameJoinRequestResolvedAsync);

		await _hubConnection.StartAsync(cancellationToken);
		_logger.LogInformation("Connected to notifications hub");
	}

	private async Task HandleGameJoinRequestReceivedAsync(GameJoinRequestReceivedDto payload)
	{
		if (OnGameJoinRequestReceived is not null)
		{
			await OnGameJoinRequestReceived(payload);
		}
	}

	private async Task HandleGameJoinRequestResolvedAsync(GameJoinRequestResolvedDto payload)
	{
		if (OnGameJoinRequestResolved is not null)
		{
			await OnGameJoinRequestResolved(payload);
		}
	}

	private string GetHubUrl((string? UserId, string? UserName, string? UserEmail)? userInfo)
	{
		var baseUrl = _configuration["Services:Api:Https:0"]
			?? _configuration["Services:Api:Http:0"]
			?? "https://localhost:7001";

		var url = $"{baseUrl}/hubs/notifications";
		if (userInfo is not null)
		{
			var queryParams = new List<string> { "authenticated=true" };
			if (!string.IsNullOrEmpty(userInfo.Value.UserId))
			{
				queryParams.Add($"userId={HttpUtility.UrlEncode(userInfo.Value.UserId)}");
			}
			if (!string.IsNullOrEmpty(userInfo.Value.UserName))
			{
				queryParams.Add($"userName={HttpUtility.UrlEncode(userInfo.Value.UserName)}");
			}
			if (!string.IsNullOrEmpty(userInfo.Value.UserEmail))
			{
				queryParams.Add($"userEmail={HttpUtility.UrlEncode(userInfo.Value.UserEmail)}");
			}
			url = $"{url}?{string.Join("&", queryParams)}";
		}

		return url;
	}

	private async Task<(string? UserId, string? UserName, string? UserEmail)?> GetUserInfoAsync()
	{
		try
		{
			var authState = await _authStateProvider.GetAuthenticationStateAsync();
			var user = authState.User;

			if (user.Identity?.IsAuthenticated != true)
			{
				return null;
			}

			var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
				?? user.FindFirstValue("sub");

			var userEmail = user.FindFirstValue(ClaimTypes.Email)
				?? user.FindFirstValue("email");

			var userName = userEmail
				?? user.FindFirstValue("preferred_username")
				?? user.Identity?.Name;

			// If the IdP doesn't expose an email claim, fall back to treating the username
			// as an email if it looks like one. This enables server-side SignalR routing
			// via IUserIdProvider when the database uses email/name as the stable key.
			userEmail ??= (!string.IsNullOrWhiteSpace(userName) && userName.Contains('@'))
				? userName
				: null;

			return (userId, userName, userEmail);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to get user info for notifications hub");
			return null;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_hubConnection is null)
		{
			return;
		}

		try
		{
			await _hubConnection.StopAsync();
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Error stopping notifications hub connection");
		}

		await _hubConnection.DisposeAsync();
		_hubConnection = null;
	}
}