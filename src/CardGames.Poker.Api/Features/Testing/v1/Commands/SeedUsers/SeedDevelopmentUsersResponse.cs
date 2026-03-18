namespace CardGames.Poker.Api.Features.Testing.v1.Commands.SeedUsers;

public sealed class SeedDevelopmentUsersResponse
{
	public int ConfiguredCount { get; init; }
	public int CreatedCount { get; init; }
	public int SkippedCount { get; init; }
	public int FailedCount { get; init; }
	public IReadOnlyList<SeedDevelopmentUserResult> Users { get; init; } = [];
}

public sealed class SeedDevelopmentUserResult
{
	public string Email { get; init; } = string.Empty;
	public string Status { get; init; } = string.Empty;
	public string? Message { get; init; }
}