using System.Security.Claims;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;

namespace CardGames.Poker.Web.Services;

/// <summary>
///     Scoped service for managing SignalR hub connections to the game server. Each Blazor circuit gets its own instance.
/// </summary>
public sealed class GameHubClient : IAsyncDisposable
{
	private readonly AuthenticationStateProvider _authStateProvider;
	private readonly IConfiguration _configuration;
	private readonly SemaphoreSlim _connectGate = new(1, 1);
	private readonly ILogger<GameHubClient> _logger;
	private int _connectionGeneration;
	private Guid? _currentGameId;

	private HubConnection? _hubConnection;
	private Func<Exception?, Task>? _onClosed;
	private Func<string?, Task>? _onReconnected;
	private Func<Exception?, Task>? _onReconnecting;

	/// <summary>
	///     Initializes a new instance of the <see cref="GameHubClient" /> class.
	/// </summary>
	public GameHubClient(
		IConfiguration configuration,
		AuthenticationStateProvider authStateProvider,
		ILogger<GameHubClient> logger)
	{
		_configuration = configuration;
		_authStateProvider = authStateProvider;
		_logger = logger;
	}

	/// <summary>
	///     Gets the current connection state.
	/// </summary>
	public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;

	/// <summary>
	///     Gets whether the client is connected.
	/// </summary>
	public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

	/// <summary>
	///     Gets whether the client is reconnecting.
	/// </summary>
	public bool IsReconnecting => _hubConnection?.State == HubConnectionState.Reconnecting;

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await _connectGate.WaitAsync();
		try
		{
			if (_hubConnection is not null)
				await DisconnectCoreAsync(CancellationToken.None);
		}
		finally
		{
			_connectGate.Release();
		}
	}

	/// <summary>
	///     Fired when the public table state is updated.
	/// </summary>
	public event Func<TableStatePublicDto, Task>? OnTableStateUpdated;

	/// <summary>
	///     Fired when the private state for the current player is updated.
	/// </summary>
	public event Func<PrivateStateDto, Task>? OnPrivateStateUpdated;

	/// <summary>
	///     Fired when a player joins the game.
	/// </summary>
	public event Func<PlayerJoinedDto, Task>? OnPlayerJoined;

	/// <summary>
	///     Fired when table settings are updated.
	/// </summary>
	public event Func<TableSettingsUpdatedDto, Task>? OnTableSettingsUpdated;

	/// <summary>
	///     Fired when a table toast notification is received.
	/// </summary>
	public event Func<TableToastNotificationDto, Task>? OnTableToastNotification;

	/// <summary>
	///     Fired when the action timer is updated.
	/// </summary>
	public event Func<ActionTimerStateDto, Task>? OnActionTimerUpdated;

	/// <summary>
	///     Fired when a player performs an action (bet, call, fold, etc.). Used to display the action temporarily in the
	///     player's seat pill.
	/// </summary>
	public event Func<PlayerActionPerformedDto, Task>? OnPlayerActionPerformed;

	/// <summary>
	///     Fired when the connection state changes.
	/// </summary>
	public event Action<HubConnectionState>? OnConnectionStateChanged;

	/// <summary>
	///     Connects to the game hub.
	/// </summary>
	public async Task ConnectAsync(CancellationToken cancellationToken = default)
	{
		while (true)
		{
			await _connectGate.WaitAsync(cancellationToken);
			try
			{
				var state = _hubConnection?.State;
				if (!GameHubConnectionPolicy.ShouldBuildNewConnection(state))
				{
					_logger.LogDebug("Hub connection already exists, state: {State}", state);
					if (state == HubConnectionState.Connected)
						return;
				}
				else
				{
					await StartNewConnectionAsync(cancellationToken);
					return;
				}
			}
			finally
			{
				_connectGate.Release();
			}

			if (await WaitForCurrentAttemptAsync(cancellationToken))
				return;
		}
	}

	/// <summary>
	///     Joins a game group to receive state updates.
	/// </summary>
	/// <param name="gameId">The game ID to join.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task JoinGameAsync(Guid gameId, CancellationToken cancellationToken = default)
	{
		if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
		{
			_logger.LogWarning("Cannot join game - not connected to hub");
			throw new InvalidOperationException("Not connected to game hub");
		}

		// Leave current game if different
		if (_currentGameId.HasValue && _currentGameId.Value != gameId)
			await LeaveGameAsync(_currentGameId.Value, cancellationToken);

		_logger.LogInformation("Joining game {GameId}", gameId);
		await _hubConnection.InvokeAsync("JoinGame", gameId, cancellationToken);
		_currentGameId = gameId;
		_logger.LogInformation("Joined game {GameId}", gameId);
	}

	/// <summary>
	///     Leaves a game group.
	/// </summary>
	/// <param name="gameId">The game ID to leave.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task LeaveGameAsync(Guid gameId, CancellationToken cancellationToken = default)
	{
		if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
		{
			_logger.LogDebug("Cannot leave game - not connected to hub");
			return;
		}

		_logger.LogInformation("Leaving game {GameId}", gameId);
		try
		{
			await _hubConnection.InvokeAsync("LeaveGame", gameId, cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Error leaving game {GameId}", gameId);
		}

		if (_currentGameId == gameId) _currentGameId = null;
	}

	/// <summary>
	///     Disconnects from the game hub.
	/// </summary>
	public async Task DisconnectAsync(CancellationToken cancellationToken = default)
	{
		await _connectGate.WaitAsync(cancellationToken);
		try
		{
			await DisconnectCoreAsync(cancellationToken);
		}
		finally
		{
			_connectGate.Release();
		}
	}

	private async Task StartNewConnectionAsync(CancellationToken cancellationToken)
	{
		await RetireCurrentConnectionAsync();

		var userInfo = await GetUserInfoAsync();
		var hubUrl = HubConnectionIdentity.HubUrl(_configuration, "/hubs/game");
		_logger.LogInformation("Connecting to game hub at {HubUrl}", hubUrl);

		var connection = new HubConnectionBuilder()
			.WithUrl(hubUrl, options =>
			{
				HubConnectionIdentity.Apply(options, _configuration, _authStateProvider);
				ApplyUnsafeHeaders(options, userInfo);
			})
			.AddJsonProtocol(options =>
			{
				// Match server-side JSON settings for proper deserialization
				options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
			})
			.WithAutomaticReconnect(new[]
			{
				TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10),
				TimeSpan.FromSeconds(30)
			})
			.Build();

		var generation = Volatile.Read(ref _connectionGeneration);
		_hubConnection = connection;
		Wire(connection, generation);

		try
		{
			await connection.StartAsync(cancellationToken);
			_logger.LogInformation("Connected to game hub");
			if (GameHubConnectionPolicy.IsCurrent(generation, Volatile.Read(ref _connectionGeneration)))
				OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to connect to game hub");
			if (ReferenceEquals(_hubConnection, connection))
				await RetireCurrentConnectionAsync();
			throw;
		}
	}

	private async Task<bool> WaitForCurrentAttemptAsync(CancellationToken cancellationToken)
	{
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var state = _hubConnection?.State;
			if (state == HubConnectionState.Connected)
				return true;
			if (state is null or HubConnectionState.Disconnected)
				return false;

			await Task.Delay(50, cancellationToken);
		}
	}

	private async Task DisconnectCoreAsync(CancellationToken cancellationToken)
	{
		if (_hubConnection is null)
			return;

		_logger.LogInformation("Disconnecting from game hub");

		try
		{
			if (_currentGameId.HasValue)
				await LeaveGameAsync(_currentGameId.Value, cancellationToken);
		}
		finally
		{
			await RetireCurrentConnectionAsync();
			_currentGameId = null;
			OnConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
		}
	}

	/// <summary>
	///     Drops the current connection so a later start is the only object that can raise events.
	///     Does not clear <see cref="_currentGameId" />.
	/// </summary>
	private async Task RetireCurrentConnectionAsync()
	{
		var connection = _hubConnection;
		if (connection is null)
			return;

		_hubConnection = null;
		Interlocked.Increment(ref _connectionGeneration);
		Unwire(connection);

		try
		{
			await connection.StopAsync();
		}
		catch (ObjectDisposedException)
		{
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Error stopping hub connection");
		}

		try
		{
			await connection.DisposeAsync();
		}
		catch (ObjectDisposedException)
		{
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Error disposing hub connection");
		}
	}

	private void Wire(HubConnection connection, int generation)
	{
		_onReconnecting = exception => OnReconnecting(exception, generation);
		_onReconnected = connectionId => OnReconnected(connectionId, generation);
		_onClosed = exception => OnClosed(exception, generation);

		connection.Reconnecting += _onReconnecting;
		connection.Reconnected += _onReconnected;
		connection.Closed += _onClosed;

		connection.On<TableStatePublicDto>("TableStateUpdated", state => HandleTableStateUpdated(state, generation));
		connection.On<PrivateStateDto>("PrivateStateUpdated", state => HandlePrivateStateUpdated(state, generation));
		connection.On<PlayerJoinedDto>("PlayerJoined", notification => HandlePlayerJoined(notification, generation));
		connection.On<TableSettingsUpdatedDto>("TableSettingsUpdated",
			notification => HandleTableSettingsUpdated(notification, generation));
		connection.On<TableToastNotificationDto>("TableToastNotification",
			notification => HandleTableToastNotification(notification, generation));
		connection.On<ActionTimerStateDto>("ActionTimerUpdated",
			timerState => HandleActionTimerUpdated(timerState, generation));
		connection.On<PlayerActionPerformedDto>("PlayerActionPerformed",
			notification => HandlePlayerActionPerformed(notification, generation));
	}

	private void Unwire(HubConnection connection)
	{
		if (_onReconnecting is not null)
		{
			connection.Reconnecting -= _onReconnecting;
			_onReconnecting = null;
		}

		if (_onReconnected is not null)
		{
			connection.Reconnected -= _onReconnected;
			_onReconnected = null;
		}

		if (_onClosed is not null)
		{
			connection.Closed -= _onClosed;
			_onClosed = null;
		}
	}

	private void ApplyUnsafeHeaders(Microsoft.AspNetCore.Http.Connections.Client.HttpConnectionOptions options,
		(string? UserId, string? UserName, string? UserEmail)? userInfo)
	{
		if (userInfo is null || !HubConnectionIdentity.AllowUnsafeHeaders(_configuration))
			return;

		options.Headers["X-User-Authenticated"] = "true";
		if (!string.IsNullOrEmpty(userInfo.Value.UserId))
			options.Headers["X-User-Id"] = userInfo.Value.UserId;
		if (!string.IsNullOrEmpty(userInfo.Value.UserName))
			options.Headers["X-User-Name"] = userInfo.Value.UserName;
		if (!string.IsNullOrEmpty(userInfo.Value.UserEmail))
			options.Headers["X-User-Email"] = userInfo.Value.UserEmail;
	}

	private async Task<(string? UserId, string? UserName, string? UserEmail)?> GetUserInfoAsync()
	{
		try
		{
			var authState = await _authStateProvider.GetAuthenticationStateAsync();
			var user = authState.User;

			if (user.Identity?.IsAuthenticated != true) return null;

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
			userEmail ??= !string.IsNullOrWhiteSpace(userName) && userName.Contains('@')
				? userName
				: null;

			return (userId, userName, userEmail);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to get user info for SignalR");
			return null;
		}
	}

	private Task OnReconnecting(Exception? exception, int handlerGeneration)
	{
		if (!GameHubConnectionPolicy.IsCurrent(handlerGeneration, Volatile.Read(ref _connectionGeneration)))
			return Task.CompletedTask;

		if (exception is not null)
			_logger.LogWarning(exception, "Hub connection lost, attempting to reconnect...");
		else
			_logger.LogInformation("Hub connection reconnecting...");

		OnConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
		return Task.CompletedTask;
	}

	private async Task OnReconnected(string? connectionId, int handlerGeneration)
	{
		if (!GameHubConnectionPolicy.IsCurrent(handlerGeneration, Volatile.Read(ref _connectionGeneration)))
			return;

		_logger.LogInformation("Hub connection restored with connection ID {ConnectionId}", connectionId);
		OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);

		// Rejoin the current game after reconnection
		if (_currentGameId.HasValue)
		{
			_logger.LogInformation("Rejoining game {GameId} after reconnection", _currentGameId.Value);
			try
			{
				await JoinGameAsync(_currentGameId.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to rejoin game {GameId} after reconnection", _currentGameId.Value);
			}
		}
	}

	private Task OnClosed(Exception? exception, int handlerGeneration)
	{
		if (!GameHubConnectionPolicy.IsCurrent(handlerGeneration, Volatile.Read(ref _connectionGeneration)))
			return Task.CompletedTask;

		if (exception is not null)
			_logger.LogError(exception, "Hub connection closed with error");
		else
			_logger.LogInformation("Hub connection closed");

		OnConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
		return Task.CompletedTask;
	}

	private async Task HandleTableStateUpdated(TableStatePublicDto state, int handlerGeneration)
	{
		if (!GameHubConnectionPolicy.IsCurrent(handlerGeneration, Volatile.Read(ref _connectionGeneration)))
			return;

		_logger.LogDebug("Received TableStateUpdated for game {GameId}", state.GameId);

		if (OnTableStateUpdated is not null) await OnTableStateUpdated(state);
	}

	private async Task HandlePrivateStateUpdated(PrivateStateDto state, int handlerGeneration)
	{
		if (!GameHubConnectionPolicy.IsCurrent(handlerGeneration, Volatile.Read(ref _connectionGeneration)))
			return;

		_logger.LogDebug("Received PrivateStateUpdated for game {GameId}, player {PlayerName}",
			state.GameId, state.PlayerName);

		if (OnPrivateStateUpdated is not null) await OnPrivateStateUpdated(state);
	}

	private async Task HandlePlayerJoined(PlayerJoinedDto notification, int handlerGeneration)
	{
		if (!GameHubConnectionPolicy.IsCurrent(handlerGeneration, Volatile.Read(ref _connectionGeneration)))
			return;

		_logger.LogDebug("Received PlayerJoined for game {GameId}, player {PlayerName} at seat {SeatIndex}",
			notification.GameId, notification.PlayerName, notification.SeatIndex);

		if (OnPlayerJoined is not null) await OnPlayerJoined(notification);
	}

	private async Task HandleTableSettingsUpdated(TableSettingsUpdatedDto notification, int handlerGeneration)
	{
		if (!GameHubConnectionPolicy.IsCurrent(handlerGeneration, Volatile.Read(ref _connectionGeneration)))
			return;

		_logger.LogDebug("Received TableSettingsUpdated for game {GameId}, updated by {UpdatedByName}",
			notification.GameId, notification.UpdatedByName);

		if (OnTableSettingsUpdated is not null) await OnTableSettingsUpdated(notification);
	}

	private async Task HandleTableToastNotification(TableToastNotificationDto notification, int handlerGeneration)
	{
		if (!GameHubConnectionPolicy.IsCurrent(handlerGeneration, Volatile.Read(ref _connectionGeneration)))
			return;

		_logger.LogDebug("Received TableToastNotification for game {GameId}: {Message}",
			notification.GameId, notification.Message);

		if (OnTableToastNotification is not null) await OnTableToastNotification(notification);
	}

	private async Task HandleActionTimerUpdated(ActionTimerStateDto timerState, int handlerGeneration)
	{
		if (!GameHubConnectionPolicy.IsCurrent(handlerGeneration, Volatile.Read(ref _connectionGeneration)))
			return;

		_logger.LogDebug(
			"Received ActionTimerUpdated: {SecondsRemaining}s remaining, seat {SeatIndex}, active: {IsActive}",
			timerState.SecondsRemaining, timerState.PlayerSeatIndex, timerState.IsActive);

		if (OnActionTimerUpdated is not null) await OnActionTimerUpdated(timerState);
	}

	private async Task HandlePlayerActionPerformed(PlayerActionPerformedDto notification, int handlerGeneration)
	{
		if (!GameHubConnectionPolicy.IsCurrent(handlerGeneration, Volatile.Read(ref _connectionGeneration)))
			return;

		_logger.LogDebug("Received PlayerActionPerformed for game {GameId}, seat {SeatIndex}: {Action}",
			notification.GameId, notification.SeatIndex, notification.ActionDescription);

		if (OnPlayerActionPerformed is not null) await OnPlayerActionPerformed(notification);
	}
}
