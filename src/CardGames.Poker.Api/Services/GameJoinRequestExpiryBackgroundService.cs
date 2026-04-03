using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services;

public sealed class GameJoinRequestExpiryBackgroundService(
	IServiceScopeFactory serviceScopeFactory,
	ILogger<GameJoinRequestExpiryBackgroundService> logger)
	: BackgroundService
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ExpirePendingJoinRequestsAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed while expiring pending game join requests.");
			}

			await Task.Delay(PollInterval, stoppingToken);
		}
	}

	private async Task ExpirePendingJoinRequestsAsync(CancellationToken cancellationToken)
	{
		using var scope = serviceScopeFactory.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var broadcaster = scope.ServiceProvider.GetRequiredService<IGameJoinRequestBroadcaster>();

		var now = DateTimeOffset.UtcNow;

		var hasExpired = await context.GameJoinRequests
			.AnyAsync(x => x.Status == GameJoinRequestStatus.Pending && x.ExpiresAt <= now, cancellationToken);

		if (!hasExpired)
		{
			return;
		}

		var expiredRequests = await context.GameJoinRequests
			.Include(x => x.Game)
			.Include(x => x.Player)
			.Where(x => x.Status == GameJoinRequestStatus.Pending && x.ExpiresAt <= now)
			.ToListAsync(cancellationToken);

		foreach (var joinRequest in expiredRequests)
		{
			joinRequest.Status = GameJoinRequestStatus.Expired;
			joinRequest.UpdatedAt = now;
			joinRequest.ResolvedAt = now;
			joinRequest.ResolvedByName = "System";
			joinRequest.ResolutionReason = "The host did not respond before the join request expired.";
		}

		await context.SaveChangesAsync(cancellationToken);

		foreach (var joinRequest in expiredRequests)
		{
			var routingKey = joinRequest.Player.Email ?? joinRequest.Player.Name;
			if (string.IsNullOrWhiteSpace(routingKey))
			{
				continue;
			}

			await broadcaster.BroadcastJoinRequestResolvedAsync(
				routingKey,
				new GameJoinRequestResolvedDto
				{
					GameId = joinRequest.GameId,
					JoinRequestId = joinRequest.Id,
					Status = joinRequest.Status.ToString(),
					HostName = joinRequest.Game.CreatedByName ?? "Host",
					PlayerName = joinRequest.Player.Name,
					ApprovedBuyIn = null,
					Reason = joinRequest.ResolutionReason,
					ResolvedAtUtc = now
				},
				cancellationToken);
		}

		logger.LogInformation("Expired {Count} pending game join requests.", expiredRequests.Count);
	}
}