using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Infrastructure.Storage;

public sealed class AvatarStorageService(BlobServiceClient blobServiceClient, IOptions<AvatarStorageOptions> options) : IAvatarStorageService
{
	private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
	private readonly AvatarStorageOptions _options = options.Value;

	public async Task<string> UploadAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken)
	{
		var containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);
		await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

		var extension = Path.GetExtension(fileName);
		if (string.IsNullOrWhiteSpace(extension))
		{
			extension = ".bin";
		}

		var blobName = $"avatars/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
		var blobClient = containerClient.GetBlobClient(blobName);

		await blobClient.UploadAsync(content, new BlobUploadOptions
		{
			HttpHeaders = new BlobHttpHeaders
			{
				ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
			}
		}, cancellationToken);

		return blobClient.Uri.ToString();
	}
}
