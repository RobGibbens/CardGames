using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;

namespace CardGames.Poker.Web.Services;

public sealed class NotificationHubClient : IAsyncDisposable
{
	private readonly IConfiguration _configuration;
	private readonly InternalApiUserTokenFactory _internalApiUserTokenFactory;
	private readonly AuthenticationStateProvider _authStateProvider;
	private readonly ILogger<NotificationHubClient> _logger;
	private HubConnection? _hubConnection;

	public event Func<GameJoinRequestReceivedDto, Task>? OnGameJoinRequestReceived;
	public event Func<GameJoinRequestResolvedDto, Task>? OnGameJoinRequestResolved;

	public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

	public NotificationHubClient(
		IConfiguration configuration,
		AuthenticationStateProvider authStateProvider,
		InternalApiUserTokenFactory internalApiUserTokenFactory,
		ILogger<NotificationHubClient> logger)
	{
		_configuration = configuration;
		_authStateProvider = authStateProvider;
		_internalApiUserTokenFactory = internalApiUserTokenFactory;
		_logger = logger;
	}

	public async Task ConnectAsync(CancellationToken cancellationToken = default)
	{
		if (_hubConnection is not null && _hubConnection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
		{
			return;
		}

		var hubUrl = GetHubUrl();

		_hubConnection = new HubConnectionBuilder()
			.WithUrl(hubUrl, options =>
			{
				options.AccessTokenProvider = () => GetAccessTokenAsync();
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

	private string GetHubUrl()
	{
		var baseUrl = _configuration["Services:Api:Https:0"]
			?? _configuration["Services:Api:Http:0"]
			?? "https://localhost:7001";

		return $"{baseUrl}/hubs/notifications";
	}

	private async Task<string?> GetAccessTokenAsync()
	{
		try
		{
			var authState = await _authStateProvider.GetAuthenticationStateAsync();
			return _internalApiUserTokenFactory.CreateToken(authState.User);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to create internal hub token");
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