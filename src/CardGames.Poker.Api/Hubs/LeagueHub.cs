using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Hubs;

/// <summary>
/// SignalR hub for real-time league management updates.
/// Clients can join per-league management groups when authorized.
/// </summary>
[Authorize(AuthenticationSchemes = HeaderAuthenticationHandler.SchemeName)]
public sealed class LeagueHub(CardsDbContext context, ILogger<LeagueHub> logger) : Hub
{
	private const string ManagedLeagueGroupPrefix = "league:managed:";
	private const string ViewedLeagueGroupPrefix = "league:viewed:";
	private const string JoinRequesterGroupPrefix = "league:join-requester:";

	/// <summary>
	/// Gets the group name used for management updates for a given league.
	/// </summary>
	public static string GetManagedLeagueGroupName(Guid leagueId) => $"{ManagedLeagueGroupPrefix}{leagueId}";

	/// <summary>
	/// Gets the group name used for viewers of a given league.
	/// </summary>
	public static string GetViewedLeagueGroupName(Guid leagueId) => $"{ViewedLeagueGroupPrefix}{leagueId}";

	/// <summary>
	/// Gets the group name used for league join-request updates for a requester.
	/// </summary>
	public static string GetJoinRequesterGroupName(string requesterUserId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(requesterUserId);
		return $"{JoinRequesterGroupPrefix}{requesterUserId}";
	}

	/// <inheritdoc />
	public override async Task OnConnectedAsync()
	{
		var userId = GetCurrentUserId();
		if (!string.IsNullOrWhiteSpace(userId))
		{
			var groupName = GetJoinRequesterGroupName(userId);
			await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

			logger.LogInformation(
				"User {UserId} (connection {ConnectionId}) joined requester group {GroupName}",
				userId,
				Context.ConnectionId,
				groupName);
		}

		await base.OnConnectedAsync();
	}

	/// <summary>
	/// Joins the caller to league management updates for the provided league.
	/// </summary>
	public async Task JoinManagedLeague(Guid leagueId)
	{
		var userId = GetCurrentUserId();
		if (string.IsNullOrWhiteSpace(userId))
		{
			throw new HubException("User identifier not found.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == leagueId
				&& x.UserId == userId
				&& x.IsActive
				&& (x.Role == LeagueRole.Owner || x.Role == LeagueRole.Manager || x.Role == LeagueRole.Admin));

		if (!canManageLeague)
		{
			logger.LogWarning("User {UserId} attempted to join unauthorized managed league group for {LeagueId}.", userId, leagueId);
			throw new HubException("Not authorized to manage this league.");
		}

		var groupName = GetManagedLeagueGroupName(leagueId);
		await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

		logger.LogInformation(
			"User {UserId} (connection {ConnectionId}) joined managed league group {GroupName}",
			userId,
			Context.ConnectionId,
			groupName);
	}

	/// <summary>
	/// Joins the caller to general league event updates for the provided league.
	/// </summary>
	public async Task JoinViewedLeague(Guid leagueId)
	{
		var userId = GetCurrentUserId();
		if (string.IsNullOrWhiteSpace(userId))
		{
			throw new HubException("User identifier not found.");
		}

		var canViewLeague = await CanViewLeagueAsync(leagueId, userId);
		if (!canViewLeague)
		{
			logger.LogWarning("User {UserId} attempted to join unauthorized viewed league group for {LeagueId}.", userId, leagueId);
			throw new HubException("Not authorized to view this league.");
		}

		var groupName = GetViewedLeagueGroupName(leagueId);
		await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

		logger.LogInformation(
			"User {UserId} (connection {ConnectionId}) joined viewed league group {GroupName}",
			userId,
			Context.ConnectionId,
			groupName);
	}

	/// <summary>
	/// Leaves league management updates for the provided league.
	/// </summary>
	public async Task LeaveManagedLeague(Guid leagueId)
	{
		var groupName = GetManagedLeagueGroupName(leagueId);
		await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

		logger.LogInformation(
			"Connection {ConnectionId} left managed league group {GroupName}",
			Context.ConnectionId,
			groupName);
	}

	/// <summary>
	/// Leaves general league event updates for the provided league.
	/// </summary>
	public async Task LeaveViewedLeague(Guid leagueId)
	{
		var groupName = GetViewedLeagueGroupName(leagueId);
		await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

		logger.LogInformation(
			"Connection {ConnectionId} left viewed league group {GroupName}",
			Context.ConnectionId,
			groupName);
	}

	private async Task<bool> CanViewLeagueAsync(Guid leagueId, string userId)
	{
		var membership = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == leagueId && x.UserId == userId && x.IsActive);

		if (membership)
		{
			return true;
		}

		return await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == leagueId && x.CreatedByUserId == userId);
	}

	private string? GetCurrentUserId()
	{
		return Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
			?? Context.User?.FindFirst("sub")?.Value
			?? Context.UserIdentifier;
	}
}
