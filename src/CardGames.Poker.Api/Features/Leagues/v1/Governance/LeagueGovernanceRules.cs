using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Leagues.v1.Governance;

public static class LeagueGovernanceRules
{
	public static LeagueRole ToCurrentUserProjectedRole(LeagueRole role)
	{
		return role == LeagueRole.Owner ? LeagueRole.Manager : role;
	}

	public static IQueryable<LeagueMemberCurrent> GovernanceCapableMembers(this IQueryable<LeagueMemberCurrent> members)
	{
		return members.Where(x => x.Role == LeagueRole.Owner || x.Role == LeagueRole.Manager || x.Role == LeagueRole.Admin);
	}

	public static bool HasAtLeastOneManager(this IEnumerable<LeagueRole> roles)
	{
		return roles.Any(IsManagerAuthority);
	}

	public static bool HasAtLeastOneAdmin(this IEnumerable<LeagueRole> roles)
	{
		return roles.Any(x => x == LeagueRole.Admin);
	}

	public static bool HasAtLeastOneGovernanceCapableMember(this IEnumerable<LeagueRole> roles)
	{
		return roles.Any(IsGovernanceCapable);
	}

	public static bool IsGovernanceCapable(LeagueRole role)
	{
		return role == LeagueRole.Admin || IsManagerAuthority(role);
	}

	public static bool IsManagerAuthority(LeagueRole role)
	{
		return role == LeagueRole.Manager || role == LeagueRole.Owner;
	}
}
