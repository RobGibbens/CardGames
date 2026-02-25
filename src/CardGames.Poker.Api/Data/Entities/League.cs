namespace CardGames.Poker.Api.Data.Entities;

public enum LeagueRole
{
	Member = 1,
	Admin = 2,
	Manager = 3,
	Owner = 4
}

public enum LeagueMembershipEventType
{
	MemberJoined = 1,
	MemberLeft = 2,
	MemberPromotedToAdmin = 3,
	MemberDemotedFromAdmin = 4,
	LeagueOwnershipTransferred = 5
}

public enum LeagueInviteStatus
{
	Active = 1,
	Revoked = 2
}

public enum LeagueJoinRequestStatus
{
	Pending = 1,
	Approved = 2,
	Denied = 3,
	Expired = 4
}

public enum LeagueSeasonStatus
{
	Planned = 1,
	InProgress = 2,
	Completed = 3
}

public enum LeagueSeasonEventStatus
{
	Planned = 1,
	Completed = 2,
	Canceled = 3
}

public enum LeagueOneOffEventType
{
	GameNight = 1,
	Tournament = 2
}

public enum LeagueOneOffEventStatus
{
	Planned = 1,
	Completed = 2,
	Canceled = 3
}

public sealed class League : EntityWithRowVersion
{
	public Guid Id { get; set; } = Guid.CreateVersion7();

	public required string Name { get; set; }

	public string? Description { get; set; }

	public required string CreatedByUserId { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public bool IsArchived { get; set; }

	public ICollection<LeagueMemberCurrent> Members { get; set; } = [];

	public ICollection<LeagueInvite> Invites { get; set; } = [];

	public ICollection<LeagueJoinRequest> JoinRequests { get; set; } = [];

	public ICollection<LeagueSeason> Seasons { get; set; } = [];

	public ICollection<LeagueSeasonEvent> SeasonEvents { get; set; } = [];

	public ICollection<LeagueOneOffEvent> OneOffEvents { get; set; } = [];

	public ICollection<LeagueSeasonEventResult> SeasonEventResults { get; set; } = [];

	public ICollection<LeagueStandingCurrent> Standings { get; set; } = [];
}

public sealed class LeagueMemberCurrent : EntityWithRowVersion
{
	public Guid LeagueId { get; set; }

	public required string UserId { get; set; }

	public LeagueRole Role { get; set; }

	public bool IsActive { get; set; }

	public DateTimeOffset JoinedAtUtc { get; set; }

	public DateTimeOffset? LeftAtUtc { get; set; }

	public DateTimeOffset UpdatedAtUtc { get; set; }

	public League League { get; set; } = null!;
}

public sealed class LeagueSeason : EntityWithRowVersion
{
	public Guid Id { get; set; } = Guid.CreateVersion7();

	public Guid LeagueId { get; set; }

	public required string Name { get; set; }

	public int? PlannedEventCount { get; set; }

	public DateTimeOffset? StartsAtUtc { get; set; }

	public DateTimeOffset? EndsAtUtc { get; set; }

	public LeagueSeasonStatus Status { get; set; }

	public required string CreatedByUserId { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public League League { get; set; } = null!;

	public ICollection<LeagueSeasonEvent> Events { get; set; } = [];
}

public sealed class LeagueSeasonEvent : EntityWithRowVersion
{
	public Guid Id { get; set; } = Guid.CreateVersion7();

	public Guid LeagueId { get; set; }

	public Guid LeagueSeasonId { get; set; }

	public required string Name { get; set; }

	public int? SequenceNumber { get; set; }

	public DateTimeOffset? ScheduledAtUtc { get; set; }

	public LeagueSeasonEventStatus Status { get; set; }

	public string? Notes { get; set; }

	public required string CreatedByUserId { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public Guid? LaunchedGameId { get; set; }

	public string? LaunchedByUserId { get; set; }

	public DateTimeOffset? LaunchedAtUtc { get; set; }

	public League League { get; set; } = null!;

	public LeagueSeason Season { get; set; } = null!;
}

public sealed class LeagueOneOffEvent : EntityWithRowVersion
{
	public Guid Id { get; set; } = Guid.CreateVersion7();

	public Guid LeagueId { get; set; }

	public required string Name { get; set; }

	public DateTimeOffset ScheduledAtUtc { get; set; }

	public LeagueOneOffEventType EventType { get; set; }

	public LeagueOneOffEventStatus Status { get; set; }

	public string? Notes { get; set; }

	public string? GameTypeCode { get; set; }

	public string? TableName { get; set; }

	public int Ante { get; set; } = 10;

	public int MinBet { get; set; } = 20;

	public required string CreatedByUserId { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public Guid? LaunchedGameId { get; set; }

	public string? LaunchedByUserId { get; set; }

	public DateTimeOffset? LaunchedAtUtc { get; set; }

	public League League { get; set; } = null!;
}

public sealed class LeagueMembershipEvent : EntityWithRowVersion
{
	public Guid Id { get; set; } = Guid.CreateVersion7();

	public Guid LeagueId { get; set; }

	public required string UserId { get; set; }

	public required string ActorUserId { get; set; }

	public LeagueMembershipEventType EventType { get; set; }

	public DateTimeOffset OccurredAtUtc { get; set; }

	public League League { get; set; } = null!;
}

public sealed class LeagueInvite : EntityWithRowVersion
{
	public Guid Id { get; set; } = Guid.CreateVersion7();

	public Guid LeagueId { get; set; }

	public required string TokenHash { get; set; }

	public string? InviteCode { get; set; }

	public LeagueInviteStatus Status { get; set; }

	public required string CreatedByUserId { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public DateTimeOffset ExpiresAtUtc { get; set; }

	public DateTimeOffset? RevokedAtUtc { get; set; }

	public string? RevokedByUserId { get; set; }

	public League League { get; set; } = null!;
}

public sealed class LeagueJoinRequest : EntityWithRowVersion
{
	public Guid Id { get; set; } = Guid.CreateVersion7();

	public Guid LeagueId { get; set; }

	public Guid InviteId { get; set; }

	public required string RequesterUserId { get; set; }

	public LeagueJoinRequestStatus Status { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public DateTimeOffset UpdatedAtUtc { get; set; }

	public DateTimeOffset ExpiresAtUtc { get; set; }

	public DateTimeOffset? ResolvedAtUtc { get; set; }

	public string? ResolvedByUserId { get; set; }

	public string? ResolutionReason { get; set; }

	public League League { get; set; } = null!;

	public LeagueInvite Invite { get; set; } = null!;
}

public sealed class LeagueSeasonEventResult : EntityWithRowVersion
{
	public Guid LeagueId { get; set; }

	public Guid LeagueSeasonId { get; set; }

	public Guid LeagueSeasonEventId { get; set; }

	public required string UserId { get; set; }

	public int Placement { get; set; }

	public int Points { get; set; }

	public int ChipsDelta { get; set; }

	public required string RecordedByUserId { get; set; }

	public DateTimeOffset RecordedAtUtc { get; set; }

	public League League { get; set; } = null!;

	public LeagueSeason Season { get; set; } = null!;

	public LeagueSeasonEvent SeasonEvent { get; set; } = null!;
}

public sealed class LeagueSeasonEventResultCorrectionAudit : EntityWithRowVersion
{
	public Guid Id { get; set; } = Guid.CreateVersion7();

	public Guid LeagueId { get; set; }

	public Guid LeagueSeasonId { get; set; }

	public Guid LeagueSeasonEventId { get; set; }

	public required string CorrectedByUserId { get; set; }

	public required string Reason { get; set; }

	public required string PreviousResultsSnapshotJson { get; set; }

	public required string NewResultsSnapshotJson { get; set; }

	public DateTimeOffset CorrectedAtUtc { get; set; }

	public League League { get; set; } = null!;

	public LeagueSeason Season { get; set; } = null!;

	public LeagueSeasonEvent SeasonEvent { get; set; } = null!;
}

public sealed class LeagueStandingCurrent : EntityWithRowVersion
{
	public Guid LeagueId { get; set; }

	public required string UserId { get; set; }

	public int TotalEvents { get; set; }

	public int TotalPoints { get; set; }

	public int TotalChipsDelta { get; set; }

	public int? LastPlacement { get; set; }

	public DateTimeOffset? LastEventRecordedAtUtc { get; set; }

	public DateTimeOffset UpdatedAtUtc { get; set; }

	public League League { get; set; } = null!;
}
