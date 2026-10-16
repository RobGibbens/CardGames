using System.Collections.Concurrent;

namespace CardGames.Poker.Api.Infrastructure.Storage;

public sealed class MemoryCardDeckStorage : ICardDeckStorageService
{
	private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

	public IReadOnlyCollection<string> Names => _blobs.Keys.ToArray();

	public async Task<string> UploadAsync(string blobName, Stream content, string contentType,
		CancellationToken cancellationToken)
	{
		using var buffer = new MemoryStream();
		await content.CopyToAsync(buffer, cancellationToken);
		_blobs[blobName] = buffer.ToArray();
		return blobName;
	}

	public Task DeleteAsync(string blobName, CancellationToken cancellationToken)
	{
		_blobs.TryRemove(blobName, out _);
		return Task.CompletedTask;
	}

	public Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken)
	{
		foreach (var name in _blobs.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
		{
			_blobs.TryRemove(name, out _);
		}

		return Task.CompletedTask;
	}

	public string GetUrl(string blobName) => $"https://card-decks.test/{blobName}";

	public Task<byte[]?> OpenReadAsync(string blobName, CancellationToken cancellationToken)
	{
		return Task.FromResult(_blobs.TryGetValue(blobName, out var content) ? content : null);
	}
}
