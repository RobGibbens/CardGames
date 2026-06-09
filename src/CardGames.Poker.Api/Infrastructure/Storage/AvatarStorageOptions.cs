namespace CardGames.Poker.Api.Infrastructure.Storage;

public sealed class AvatarStorageOptions
{
	public const string SectionName = "AvatarStorage";

	public string ContainerName { get; } = "avatars";

	public string? PublicBaseUrl { get; set; }

	public long MaxUploadBytes { get; } = 5 * 1024 * 1024;
}