using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Leagues.v1.Governance;

public static class LeagueGovernanceRules
{
	public static IQueryable<LeagueMemberCurrent> GovernanceCapableMembers(this IQueryable<LeagueMemberCurrent> members)
	{
		return members.Where(x => x.Role == LeagueRole.Manager || x.Role == LeagueRole.Admin);
	}

	public static bool HasAtLeastOneManager(this IEnumerable<LeagueRole> roles)
	{
		return roles.Any(x => x == LeagueRole.Manager);
	}

	public static bool HasAtLeastOneGovernanceCapableMember(this IEnumerable<LeagueRole> roles)
	{
		return roles.Any(IsGovernanceCapable);
	}

	public static bool IsGovernanceCapable(LeagueRole role)
	{
		return role == LeagueRole.Manager || role == LeagueRole.Admin;
	}
}
