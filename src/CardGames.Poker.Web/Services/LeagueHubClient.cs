using CardGames.Contracts.SignalR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using System.Security.Claims;
using System.Web;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Scoped service for managing SignalR hub connections to league management updates.
/// Each Blazor circuit gets its own instance.
/// </summary>
public sealed class LeagueHubClient : IAsyncDisposable
{
	private readonly IConfiguration _configuration;
	private readonly AuthenticationStateProvider _authStateProvider;
	private readonly ILogger<LeagueHubClient> _logger;

	private HubConnection? _hubConnection;
	private HashSet<Guid> _managedLeagueIds = [];

	/// <summary>
	/// Fired when a league join request is submitted.
	/// </summary>
	public event Func<LeagueJoinRequestSubmittedDto, Task>? OnLeagueJoinRequestSubmitted;

	/// <summary>
	/// Fired when a league join request is updated.
	/// </summary>
	public event Func<LeagueJoinRequestUpdatedDto, Task>? OnLeagueJoinRequestUpdated;

	/// <summary>
	/// Gets whether the client is connected.
	/// </summary>
	public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

	/// <summary>
	/// Initializes a new instance of the <see cref="LeagueHubClient"/> class.
	/// </summary>
	public LeagueHubClient(
		IConfiguration configuration,
		AuthenticationStateProvider authStateProvider,
		ILogger<LeagueHubClient> logger)
	{
		_configuration = configuration;
		_authStateProvider = authStateProvider;
		_logger = logger;
	}

	/// <summary>
	/// Connects to the leagues hub.
	/// </summary>
	public async Task ConnectAsync(CancellationToken cancellationToken = default)
	{
		if (_hubConnection is not null)
		{
			if (_hubConnection.State == HubConnectionState.Connected)
			{
				return;
			}

			if (_hubConnection.State == HubConnectionState.Connecting || _hubConnection.State == HubConnectionState.Reconnecting)
			{
				return;
			}
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

		_hubConnection.Reconnected += OnReconnected;
		_hubConnection.Closed += OnClosed;

		_hubConnection.On<LeagueJoinRequestSubmittedDto>("LeagueJoinRequestSubmitted", HandleLeagueJoinRequestSubmittedAsync);
		_hubConnection.On<LeagueJoinRequestUpdatedDto>("LeagueJoinRequestUpdated", HandleLeagueJoinRequestUpdatedAsync);

		await _hubConnection.StartAsync(cancellationToken);
		await JoinManagedLeaguesAsync(_managedLeagueIds, cancellationToken);

		_logger.LogInformation("Connected to leagues hub");
	}

	/// <summary>
	/// Sets the leagues that should receive management updates.
	/// </summary>
	public async Task SetManagedLeaguesAsync(IEnumerable<Guid> leagueIds, CancellationToken cancellationToken = default)
	{
		var target = leagueIds.Distinct().ToHashSet();

		if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
		{
			_managedLeagueIds = target;
			return;
		}

		var toLeave = _managedLeagueIds.Except(target).ToList();
		var toJoin = target.Except(_managedLeagueIds).ToList();

		foreach (var leagueId in toLeave)
		{
			await LeaveManagedLeagueAsync(leagueId, cancellationToken);
		}

		foreach (var leagueId in toJoin)
		{
			await JoinManagedLeagueAsync(leagueId, cancellationToken);
		}

		_managedLeagueIds = target;
	}

	/// <summary>
	/// Disconnects from the leagues hub.
	/// </summary>
	public async Task DisconnectAsync(CancellationToken cancellationToken = default)
	{
		if (_hubConnection is null)
		{
			return;
		}

		if (_hubConnection.State == HubConnectionState.Connected)
		{
			foreach (var leagueId in _managedLeagueIds)
			{
				try
				{
					await LeaveManagedLeagueAsync(leagueId, cancellationToken);
				}
				catch (Exception ex)
				{
					_logger.LogDebug(ex, "Failed leaving managed league {LeagueId} while disconnecting", leagueId);
				}
			}
		}

		try
		{
			await _hubConnection.StopAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Error stopping leagues hub connection");
		}
	}

	private async Task JoinManagedLeaguesAsync(IEnumerable<Guid> leagueIds, CancellationToken cancellationToken)
	{
		foreach (var leagueId in leagueIds)
		{
			await JoinManagedLeagueAsync(leagueId, cancellationToken);
		}
	}

	private async Task JoinManagedLeagueAsync(Guid leagueId, CancellationToken cancellationToken)
	{
		if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
		{
			return;
		}

		await _hubConnection.InvokeAsync("JoinManagedLeague", leagueId, cancellationToken);
	}

	private async Task LeaveManagedLeagueAsync(Guid leagueId, CancellationToken cancellationToken)
	{
		if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
		{
			return;
		}

		await _hubConnection.InvokeAsync("LeaveManagedLeague", leagueId, cancellationToken);
	}

	private async Task HandleLeagueJoinRequestSubmittedAsync(LeagueJoinRequestSubmittedDto payload)
	{
		if (OnLeagueJoinRequestSubmitted is not null)
		{
			await OnLeagueJoinRequestSubmitted(payload);
		}
	}

	private async Task HandleLeagueJoinRequestUpdatedAsync(LeagueJoinRequestUpdatedDto payload)
	{
		if (OnLeagueJoinRequestUpdated is not null)
		{
			await OnLeagueJoinRequestUpdated(payload);
		}
	}

	private async Task OnReconnected(string? connectionId)
	{
		_logger.LogInformation("Leagues hub connection restored with connection ID {ConnectionId}", connectionId);

		try
		{
			await JoinManagedLeaguesAsync(_managedLeagueIds, CancellationToken.None);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to rejoin managed league groups after reconnection");
		}
	}

	private Task OnClosed(Exception? exception)
	{
		if (exception is not null)
		{
			_logger.LogWarning(exception, "Leagues hub connection closed with error");
		}
		else
		{
			_logger.LogInformation("Leagues hub connection closed");
		}

		return Task.CompletedTask;
	}

	private string GetHubUrl((string? UserId, string? UserName)? userInfo)
	{
		var baseUrl = _configuration["Services:Api:Https:0"]
			?? _configuration["Services:Api:Http:0"]
			?? "https://localhost:7001";

		var url = $"{baseUrl}/hubs/leagues";

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
			url = $"{url}?{string.Join("&", queryParams)}";
		}

		return url;
	}

	private async Task<(string? UserId, string? UserName)?> GetUserInfoAsync()
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

			var userName = user.FindFirstValue(ClaimTypes.Email)
						   ?? user.FindFirstValue("email")
						   ?? user.FindFirstValue("preferred_username")
						   ?? user.Identity?.Name;

			return (userId, userName);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to get user info for SignalR");
			return null;
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_hubConnection is null)
		{
			return;
		}

		_hubConnection.Reconnected -= OnReconnected;
		_hubConnection.Closed -= OnClosed;

		await DisconnectAsync();
		await _hubConnection.DisposeAsync();
		_hubConnection = null;
	}
}
