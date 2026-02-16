namespace CardGames.Poker.Api.Infrastructure.Storage;

public sealed class AvatarStorageOptions
{
	public const string SectionName = "AvatarStorage";

	public string ContainerName { get; set; } = "avatars";

	public long MaxUploadBytes { get; set; } = 5 * 1024 * 1024;
}
