using Refit;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

public partial interface ILeaguesApi
{
	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues")]
	Task<IApiResponse<CreateLeagueResponse>> CreateLeagueAsync([Body] CreateLeagueRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/mine")]
	Task<IApiResponse<IReadOnlyList<LeagueSummaryDto>>> GetMyLeaguesAsync(CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}")]
	Task<IApiResponse<LeagueDetailDto>> GetLeagueDetailAsync(Guid leagueId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/members")]
	Task<IApiResponse<IReadOnlyList<LeagueMemberDto>>> GetLeagueMembersAsync(Guid leagueId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/active-games")]
	Task<IApiResponse<LeagueActiveGamesPageDto>> GetActiveGamesPageAsync(
		Guid leagueId,
		[AliasAs("pageSize")][Query] int? pageSize = 5,
		[AliasAs("pageNumber")][Query] int? pageNumber = 1,
		CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/members/history")]
	Task<IApiResponse<IReadOnlyList<LeagueMembershipHistoryItemDto>>> GetMembershipHistoryAsync(Guid leagueId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/invites")]
	Task<IApiResponse<CreateLeagueInviteResponse>> CreateInviteAsync(Guid leagueId, [Body] CreateLeagueInviteRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/invites")]
	Task<IApiResponse<IReadOnlyList<LeagueInviteSummaryDto>>> GetInvitesAsync(Guid leagueId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/leagues/{leagueId}/invites/{inviteId}/revoke")]
	Task<IApiResponse> RevokeInviteAsync(Guid leagueId, Guid inviteId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/join")]
	Task<IApiResponse<JoinLeagueResponse>> JoinLeagueAsync([Body] JoinLeagueRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/join-requests/pending")]
	Task<IApiResponse<IReadOnlyList<LeagueJoinRequestQueueItemDto>>> GetPendingJoinRequestsAsync(Guid leagueId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/join-requests/{joinRequestId}/approve")]
	Task<IApiResponse> ApproveJoinRequestAsync(Guid leagueId, Guid joinRequestId, [Body] ModerateLeagueJoinRequestRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/join-requests/{joinRequestId}/deny")]
	Task<IApiResponse> DenyJoinRequestAsync(Guid leagueId, Guid joinRequestId, [Body] ModerateLeagueJoinRequestRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/leagues/{leagueId}/leave")]
	Task<IApiResponse<LeaveLeagueResponse>> LeaveLeagueAsync(Guid leagueId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/leagues/{leagueId}/members/{memberUserId}/promote-admin")]
	Task<IApiResponse> PromoteMemberToAdminAsync(Guid leagueId, string memberUserId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/leagues/{leagueId}/members/{memberUserId}/demote-admin")]
	Task<IApiResponse> DemoteAdminToMemberAsync(Guid leagueId, string memberUserId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/leagues/{leagueId}/members/{memberUserId}/transfer-ownership")]
	Task<IApiResponse> TransferOwnershipAsync(Guid leagueId, string memberUserId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/leagues/{leagueId}/members/{memberUserId}/remove")]
	Task<IApiResponse> RemoveMemberAsync(Guid leagueId, string memberUserId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/join-preview")]
	Task<IApiResponse<LeagueJoinPreviewDto>> GetJoinPreviewAsync([AliasAs("token")] string token, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/seasons")]
	Task<IApiResponse<CreateLeagueSeasonResponse>> CreateSeasonAsync(Guid leagueId, [Body] CreateLeagueSeasonRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/seasons")]
	Task<IApiResponse<IReadOnlyList<LeagueSeasonDto>>> GetSeasonsAsync(Guid leagueId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/seasons/{seasonId}/events")]
	Task<IApiResponse<CreateLeagueSeasonEventResponse>> CreateSeasonEventAsync(Guid leagueId, Guid seasonId, [Body] CreateLeagueSeasonEventRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Put("/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}")]
	Task<IApiResponse> UpdateSeasonEventAsync(Guid leagueId, Guid seasonId, Guid eventId, [Body] UpdateLeagueSeasonEventRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Delete("/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}")]
	Task<IApiResponse> DeleteSeasonEventAsync(Guid leagueId, Guid seasonId, Guid eventId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/seasons/{seasonId}/events")]
	Task<IApiResponse<IReadOnlyList<LeagueSeasonEventDto>>> GetSeasonEventsAsync(Guid leagueId, Guid seasonId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/recent-completed")]
	Task<IApiResponse<IReadOnlyList<LeagueSeasonEventDto>>> GetRecentCompletedSeasonEventsAsync(
		Guid leagueId,
		Guid seasonId,
		[AliasAs("take")][Query] int? take = 5,
		CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}/results")]
	Task<IApiResponse> IngestSeasonEventResultsAsync(Guid leagueId, Guid seasonId, Guid eventId, [Body] IngestLeagueSeasonEventResultsRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}/launch")]
	Task<IApiResponse<LaunchLeagueEventSessionResponse>> LaunchSeasonEventSessionAsync(Guid leagueId, Guid seasonId, Guid eventId, [Body] LaunchLeagueEventSessionRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/events/one-off")]
	Task<IApiResponse<CreateLeagueOneOffEventResponse>> CreateOneOffEventAsync(Guid leagueId, [Body] CreateLeagueOneOffEventRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Put("/api/v1/leagues/{leagueId}/events/one-off/{eventId}")]
	Task<IApiResponse> UpdateOneOffEventAsync(Guid leagueId, Guid eventId, [Body] UpdateLeagueOneOffEventRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Delete("/api/v1/leagues/{leagueId}/events/one-off/{eventId}")]
	Task<IApiResponse> DeleteOneOffEventAsync(Guid leagueId, Guid eventId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/events/one-off")]
	Task<IApiResponse<IReadOnlyList<LeagueOneOffEventDto>>> GetOneOffEventsAsync(Guid leagueId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/events/upcoming")]
	Task<IApiResponse<LeagueUpcomingEventsPageDto>> GetUpcomingEventsPageAsync(
		Guid leagueId,
		[AliasAs("pageSize")][Query] int? pageSize = 5,
		[AliasAs("pageNumber")][Query] int? pageNumber = 1,
		CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/standings")]
	Task<IApiResponse<IReadOnlyList<LeagueStandingEntryDto>>> GetLeagueStandingsAsync(Guid leagueId, [AliasAs("seasonId")] Guid? seasonId = null, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/events/one-off/{eventId}/launch")]
	Task<IApiResponse<LaunchLeagueEventSessionResponse>> LaunchOneOffEventSessionAsync(Guid leagueId, Guid eventId, [Body] LaunchLeagueEventSessionRequest request, CancellationToken cancellationToken = default);
}
