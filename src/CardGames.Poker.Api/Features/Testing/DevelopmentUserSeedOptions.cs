namespace CardGames.Poker.Api.Features.Testing;

public sealed class DevelopmentUserSeedOptions
{
	public const string SectionName = "DevelopmentUserSeed";

	public List<DevelopmentSeedUser> Users { get; init; } = [];
}

public sealed class DevelopmentSeedUser
{
	public string Email { get; init; } = string.Empty;
	public string Password { get; init; } = string.Empty;
	public string FirstName { get; init; } = string.Empty;
	public string LastName { get; init; } = string.Empty;
	public string? PhoneNumber { get; init; }
	public string? AvatarUrl { get; init; }
}