namespace CardGames.Poker.Api.Infrastructure.Storage;

public interface IAvatarStorageService
{
	Task<string> UploadAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken);
}
