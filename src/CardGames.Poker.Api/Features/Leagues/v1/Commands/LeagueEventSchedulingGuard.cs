namespace CardGames.Poker.Api.Features.Leagues.v1.Commands;

internal static class LeagueEventSchedulingGuard
{
	public const string ScheduledAtUtcMustBeInFutureMessage = "Scheduled date/time must be in the future.";

	public static bool IsScheduledAtInFuture(DateTimeOffset scheduledAtUtc)
	{
		return scheduledAtUtc > DateTimeOffset.UtcNow;
	}
}