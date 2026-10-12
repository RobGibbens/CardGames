using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Web.Services;

/// <summary>
///     Decides whether an action-timer tick belongs to an earlier hand or an earlier countdown.
/// </summary>
public static class ActionTimerUpdatePolicy
{
	public static bool ShouldIgnore(
		ActionTimerStateDto incoming,
		int displayedHandNumber,
		ulong appliedStateVersion,
		ActionTimerStateDto? displayedTimer)
	{
		if (incoming.HandNumber > 0 && displayedHandNumber > 0 && incoming.HandNumber < displayedHandNumber)
			return true;

		if (incoming.StateVersion > 0 && appliedStateVersion > 0 && incoming.StateVersion < appliedStateVersion)
			return true;

		if (displayedTimer is { IsActive: true } && incoming.StartedAtUtc < displayedTimer.StartedAtUtc)
			return true;

		return false;
	}
}
