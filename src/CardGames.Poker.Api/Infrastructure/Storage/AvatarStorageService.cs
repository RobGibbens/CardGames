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

	public async Task<AvatarStorageFile?> TryGetAsync(string avatarReference, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(avatarReference))
		{
			return null;
		}

		var blobName = ResolveBlobName(avatarReference);
		if (string.IsNullOrWhiteSpace(blobName))
		{
			return null;
		}

		var containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);
		var blobClient = containerClient.GetBlobClient(blobName);

		try
		{
			if (!await blobClient.ExistsAsync(cancellationToken))
			{
				return null;
			}

			var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
			await using var content = response.Value.Content;
			using var memoryStream = new MemoryStream();
			await content.CopyToAsync(memoryStream, cancellationToken);

			var contentType = response.Value.Details.ContentType;
			if (string.IsNullOrWhiteSpace(contentType))
			{
				contentType = "application/octet-stream";
			}

			return new AvatarStorageFile(memoryStream.ToArray(), contentType);
		}
		catch (Azure.RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
		{
			return null;
		}
	}

	private string? ResolveBlobName(string avatarReference)
	{
		avatarReference = avatarReference.Trim();

		if (!Uri.TryCreate(avatarReference, UriKind.Absolute, out var absoluteUri))
		{
			if (avatarReference.StartsWith($"{_options.ContainerName}/", StringComparison.OrdinalIgnoreCase))
			{
				return avatarReference[(_options.ContainerName.Length + 1)..];
			}

			return avatarReference.TrimStart('/');
		}

		var absolutePath = absoluteUri.AbsolutePath.Trim('/');
		if (absolutePath.StartsWith($"{_options.ContainerName}/", StringComparison.OrdinalIgnoreCase))
		{
			return absolutePath[(_options.ContainerName.Length + 1)..];
		}

		var pathSegments = absolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (pathSegments.Length >= 2 && string.Equals(pathSegments[0], _options.ContainerName, StringComparison.OrdinalIgnoreCase))
		{
			return string.Join('/', pathSegments.Skip(1));
		}

		var containerSegmentIndex = Array.FindIndex(pathSegments,
			segment => string.Equals(segment, _options.ContainerName, StringComparison.OrdinalIgnoreCase));
		if (containerSegmentIndex >= 0 && containerSegmentIndex < pathSegments.Length - 1)
		{
			return string.Join('/', pathSegments.Skip(containerSegmentIndex + 1));
		}

		return null;
	}
}
