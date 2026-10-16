namespace CardGames.Poker.Api.Infrastructure.Storage;

public interface ICardDeckStorageService
{
	Task<string> UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken);

	Task DeleteAsync(string blobName, CancellationToken cancellationToken);

	Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken);

	string GetUrl(string blobName);

	Task<byte[]?> OpenReadAsync(string blobName, CancellationToken cancellationToken);
}
