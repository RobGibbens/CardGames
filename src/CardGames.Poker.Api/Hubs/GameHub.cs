using System.Security.Claims;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Hubs;

/// <summary>
///     SignalR hub for real-time poker game state updates.
///     Clients join per-game groups to receive table state broadcasts.
/// </summary>
[Authorize(AuthenticationSchemes = InternalUserTokenAuthenticationHandler.HubSchemes)]
public sealed class GameHub : Hub
{
	/// <summary>
	///     Group name prefix for game groups.
	/// </summary>
	private const string GameGroupPrefix = "game:";

	private readonly ILogger<GameHub> _logger;
	private readonly GameEventsTelemetry _telemetry;
	private readonly ITableStateBuilder _tableStateBuilder;
	private readonly IGamePlayerResolver _gamePlayerResolver;
	private readonly CardsDbContext _context;

	/// <summary>
	///     Initializes a new instance of the <see cref="GameHub" /> class.
	/// </summary>
	public GameHub(ITableStateBuilder tableStateBuilder, CardsDbContext context, GameEventsTelemetry telemetry,
		IGamePlayerResolver gamePlayerResolver, ILogger<GameHub> logger)
	{
		_tableStateBuilder = tableStateBuilder;
		_context = context;
		_telemetry = telemetry;
		_gamePlayerResolver = gamePlayerResolver;
		_logger = logger;
	}

	/// <summary>
	///     Joins the caller to the specified game group and sends initial state snapshot.
	/// </summary>
	/// <param name="gameId">The unique identifier of the game to join.</param>
	public async Task JoinGame(Guid gameId)
	{
		var userId = Context.UserIdentifier ?? GetUserIdentifier();
		using var scope = CreateScope(userId, gameId);
		if (string.IsNullOrEmpty(userId))
		{
			_logger.LogWarning("User attempted to join game {GameId} without valid identifier", gameId);
			throw new HubException("User identifier not found");
		}

		var seated = await _gamePlayerResolver.ResolveSeatedPlayerAsync(gameId, Context.User ?? new ClaimsPrincipal(),
			Context.ConnectionAborted);
		if (seated is null)
		{
			_logger.LogWarning("User {UserId} attempted to join game {GameId} without a seat", userId, gameId);
			throw new HubException("Not seated at this table");
		}

		var privateStateUserId = seated.Player.Email ?? seated.Player.Name ?? seated.Player.ExternalId;
		if (string.IsNullOrWhiteSpace(privateStateUserId))
			throw new HubException("Not seated at this table");

		var groupName = GetGroupName(gameId);
		var game = await _context.Games
			.Include(g => g.GameType)
			.AsNoTracking()
			.FirstOrDefaultAsync(g => g.Id == gameId);

		// Add the caller to the game group
		await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
		if (game is not null)
		{
			_telemetry.RecordPlayerEvent(GameEventsTelemetry.ResolveGameType(game),
				GameEventsTelemetry.ResolveTableKind(game), "reconnected");
		}

		_logger.LogInformation(
			"User {UserId} (connection {ConnectionId}) joined game group {GroupName}",
			userId, Context.ConnectionId, groupName);

		// Send initial state snapshot to the caller. Private cards use the seated player's identity.
		await SendStateSnapshotToCallerAsync(gameId, privateStateUserId);
	}

	/// <summary>
	///     Removes the caller from the specified game group.
	/// </summary>
	/// <param name="gameId">The unique identifier of the game to leave.</param>
	public async Task LeaveGame(Guid gameId)
	{
		var userId = Context.UserIdentifier ?? GetUserIdentifier();
		using var scope = CreateScope(userId, gameId);
		var groupName = GetGroupName(gameId);

		await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

		_logger.LogInformation(
			"User {UserId} (connection {ConnectionId}) left game group {GroupName}",
			userId ?? "unknown", Context.ConnectionId, groupName);
	}

	/// <inheritdoc />
	public override async Task OnConnectedAsync()
	{
		var userId = Context.UserIdentifier ?? GetUserIdentifier();
		using var scope = CreateScope(userId);
		_logger.LogInformation("User {UserId} connected with connection {ConnectionId}",
			userId ?? "unknown", Context.ConnectionId);

		await base.OnConnectedAsync();
	}

	/// <inheritdoc />
	public override async Task OnDisconnectedAsync(Exception? exception)
	{
		var userId = Context.UserIdentifier ?? GetUserIdentifier();
		using var scope = CreateScope(userId);

		if (exception is not null)
			_logger.LogWarning(exception,
				"User {UserId} disconnected with error (connection {ConnectionId})",
				userId ?? "unknown", Context.ConnectionId);
		else
			_logger.LogInformation("User {UserId} disconnected (connection {ConnectionId})",
				userId ?? "unknown", Context.ConnectionId);

		_telemetry.RecordPlayerEvent("unknown", "connection", "disconnected");

		await base.OnDisconnectedAsync(exception);
	}

	/// <summary>
	///     Sends the current state snapshot to the calling client.
	/// </summary>
	private async Task SendStateSnapshotToCallerAsync(Guid gameId, string userId)
	{
		try
		{
			_logger.LogDebug(
				"Sending state snapshot for game {GameId} to caller user {UserId} (connection {ConnectionId})",
				gameId, userId, Context.ConnectionId);

			// Build and send public state
			var publicState = await _tableStateBuilder.BuildPublicStateAsync(gameId);
			if (publicState is not null) await Clients.Caller.SendAsync("TableStateUpdated", publicState);

			// Build and send private state for this user.
			// Prefer SignalR's stable user id, but fall back to email/name matching for legacy connections.
			var privateStateUserId = Context.UserIdentifier ?? userId;
			var privateState = await _tableStateBuilder.BuildPrivateStateAsync(gameId, privateStateUserId);
			if (privateState is not null) await Clients.Caller.SendAsync("PrivateStateUpdated", privateState);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error sending state snapshot for game {GameId} to user {UserId}",
				gameId, userId);
			throw new HubException("Failed to retrieve game state");
		}
	}

	/// <summary>
	///     Gets the stable user identifier from the connection context.
	///     Uses email claim to match existing identity usage in the application.
	/// </summary>
	private string? GetUserIdentifier()
	{
		// Match the claim order used by CurrentUserService.UserName
		return Context.User?.FindFirst(ClaimTypes.Email)?.Value
		       ?? Context.User?.FindFirst("email")?.Value
		       ?? Context.User?.FindFirst("preferred_username")?.Value
		       ?? Context.User?.Identity?.Name;
	}

	private IDisposable? CreateScope(string? userId, Guid? gameId = null)
	{
		var values = new Dictionary<string, object>
		{
			["ConnectionId"] = Context.ConnectionId,
			["UserId"] = userId ?? "anonymous"
		};

		if (gameId.HasValue)
		{
			values["GameId"] = gameId.Value;
		}

		return _logger.BeginScope(values);
	}

	/// <summary>
	///     Gets the SignalR group name for a game.
	/// </summary>
	private static string GetGroupName(Guid gameId)
	{
		return $"{GameGroupPrefix}{gameId}";
	}
}