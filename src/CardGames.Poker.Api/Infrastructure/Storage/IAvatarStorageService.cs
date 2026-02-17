namespace CardGames.Poker.Api.Infrastructure.Storage;

public interface IAvatarStorageService
{
	Task<string> UploadAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken);
	Task<AvatarStorageFile?> TryGetAsync(string avatarReference, CancellationToken cancellationToken);
}

public sealed record AvatarStorageFile(byte[] Content, string ContentType);
