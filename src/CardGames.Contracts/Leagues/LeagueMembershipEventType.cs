using System.ComponentModel;

namespace CardGames.Poker.Api.Contracts;

public enum LeagueMembershipHistoryEventType
{
	[Description("Member joined")]
	MemberJoined = 1,
	[Description("Member left")]
	MemberLeft = 2,
	[Description("Member promoted to admin")]
	MemberPromotedToAdmin = 3
}
