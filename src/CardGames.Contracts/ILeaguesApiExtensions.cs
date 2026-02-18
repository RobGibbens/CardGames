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
	[Post("/api/v1/leagues/{leagueId}/leave")]
	Task<IApiResponse<LeaveLeagueResponse>> LeaveLeagueAsync(Guid leagueId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/leagues/{leagueId}/members/{memberUserId}/promote-admin")]
	Task<IApiResponse> PromoteMemberToAdminAsync(Guid leagueId, string memberUserId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/seasons")]
	Task<IApiResponse<CreateLeagueSeasonResponse>> CreateSeasonAsync(Guid leagueId, [Body] CreateLeagueSeasonRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/seasons")]
	Task<IApiResponse<IReadOnlyList<LeagueSeasonDto>>> GetSeasonsAsync(Guid leagueId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/seasons/{seasonId}/events")]
	Task<IApiResponse<CreateLeagueSeasonEventResponse>> CreateSeasonEventAsync(Guid leagueId, Guid seasonId, [Body] CreateLeagueSeasonEventRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/seasons/{seasonId}/events")]
	Task<IApiResponse<IReadOnlyList<LeagueSeasonEventDto>>> GetSeasonEventsAsync(Guid leagueId, Guid seasonId, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/leagues/{leagueId}/events/one-off")]
	Task<IApiResponse<CreateLeagueOneOffEventResponse>> CreateOneOffEventAsync(Guid leagueId, [Body] CreateLeagueOneOffEventRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/leagues/{leagueId}/events/one-off")]
	Task<IApiResponse<IReadOnlyList<LeagueOneOffEventDto>>> GetOneOffEventsAsync(Guid leagueId, CancellationToken cancellationToken = default);
}