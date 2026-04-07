using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CorrectLeagueSeasonEventResults;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueInvite;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueOneOffEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeasonEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.DeleteLeagueOneOffEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.DeleteLeagueSeasonEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueOneOffEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueSeasonEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.DenyLeagueJoinRequest;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.LaunchLeagueEventSession;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.JoinLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.LeaveLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.ApproveLeagueJoinRequest;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.PromoteLeagueMemberToAdmin;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.DemoteLeagueAdminToMember;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.RemoveLeagueMember;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.RevokeLeagueInvite;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.TransferLeagueOwnership;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueDetail;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueInvites;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueJoinPreview;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueMembershipHistory;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueMembers;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueActiveGamesPage;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueOneOffEvents;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueUpcomingEventsPage;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasonEvents;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasons;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueStandings;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetPendingLeagueJoinRequests;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetRecentCompletedLeagueSeasonEvents;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetMyLeagues;

namespace CardGames.Poker.Api.Features.Leagues.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/leagues")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name]);

		mapGroup.MapCreateLeague();
		mapGroup.MapGetMyLeagues();
		mapGroup.MapGetLeagueDetail();
		mapGroup.MapGetLeagueMembers();
		mapGroup.MapGetLeagueActiveGamesPage();
		mapGroup.MapGetLeagueMembershipHistory();
		mapGroup.MapCreateLeagueInvite();
		mapGroup.MapRevokeLeagueInvite();
		mapGroup.MapGetLeagueInvites();
		mapGroup.MapGetLeagueJoinPreview();
		mapGroup.MapJoinLeague();
		mapGroup.MapGetPendingLeagueJoinRequests();
		mapGroup.MapApproveLeagueJoinRequest();
		mapGroup.MapDenyLeagueJoinRequest();
		mapGroup.MapLeaveLeague();
		mapGroup.MapPromoteLeagueMemberToAdmin();
		mapGroup.MapDemoteLeagueAdminToMember();
		mapGroup.MapTransferLeagueOwnership();
		mapGroup.MapRemoveLeagueMember();
		mapGroup.MapCreateLeagueSeason();
		mapGroup.MapGetLeagueSeasons();
		mapGroup.MapCreateLeagueSeasonEvent();
		mapGroup.MapUpdateLeagueSeasonEvent();
		mapGroup.MapDeleteLeagueSeasonEvent();
		mapGroup.MapGetLeagueSeasonEvents();
		mapGroup.MapGetRecentCompletedLeagueSeasonEvents();
		mapGroup.MapIngestLeagueSeasonEventResults();
		mapGroup.MapCorrectLeagueSeasonEventResults();
		mapGroup.MapCreateLeagueOneOffEvent();
		mapGroup.MapUpdateLeagueOneOffEvent();
		mapGroup.MapDeleteLeagueOneOffEvent();
		mapGroup.MapGetLeagueOneOffEvents();
		mapGroup.MapGetLeagueUpcomingEventsPage();
		mapGroup.MapGetLeagueStandings();
		mapGroup.MapLaunchLeagueEventSession();
	}
}