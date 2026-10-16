using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Infrastructure.Storage;

public sealed class CardDeckStorageService(BlobServiceClient blobServiceClient, IOptions<CardDeckStorageOptions> options)
	: ICardDeckStorageService
{
	private readonly CardDeckStorageOptions _options = options.Value;

	public async Task<string> UploadAsync(string blobName, Stream content, string contentType,
		CancellationToken cancellationToken)
	{
		var containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
		await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

		var blobClient = containerClient.GetBlobClient(blobName);
		await blobClient.UploadAsync(content, new BlobUploadOptions
		{
			HttpHeaders = new BlobHttpHeaders
			{
				ContentType = contentType
			}
		}, cancellationToken);

		return blobClient.Name;
	}

	public async Task DeleteAsync(string blobName, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(blobName))
		{
			return;
		}

		var containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
		var blobClient = containerClient.GetBlobClient(blobName);
		await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
	}

	public async Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken)
	{
		var containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
		if (!await containerClient.ExistsAsync(cancellationToken))
		{
			return;
		}

		await foreach (var blob in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
		{
			await containerClient.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: cancellationToken);
		}
	}

	public string GetUrl(string blobName)
	{
		var containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
		return containerClient.GetBlobClient(blobName).Uri.ToString();
	}

	public async Task<byte[]?> OpenReadAsync(string blobName, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(blobName))
		{
			return null;
		}

		var containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
		var blobClient = containerClient.GetBlobClient(blobName);
		try
		{
			if (!await blobClient.ExistsAsync(cancellationToken))
			{
				return null;
			}

			var response = await blobClient.DownloadContentAsync(cancellationToken);
			return response.Value.Content.ToArray();
		}
		catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
		{
			return null;
		}
	}
}
