using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.Poker.Api.Contracts;
using Microsoft.AspNetCore.Components;
using Refit;

namespace CardGames.Poker.Web.Services;

public static class CardDeckImages
{
	public static SelectedCardDeckImagesDto BuiltIn()
	{
		return new SelectedCardDeckImagesDto
		{
			Name = CardDeckSlots.BuiltInName,
			BackUrl = CardDeckSlots.BuiltInBackUrl,
			Faces = CardDeckSlots.Faces().ToDictionary(
				face => CardDeckSlots.FaceKey(face.Suit, face.Rank),
				face => CardDeckSlots.BuiltInFaceUrl(face.Suit, face.Rank),
				StringComparer.Ordinal)
		};
	}

	public static string Back(SelectedCardDeckImagesDto? deck, NavigationManager navigation)
	{
		var url = string.IsNullOrWhiteSpace(deck?.BackUrl) ? CardDeckSlots.BuiltInBackUrl : deck.BackUrl;
		return Rewrite(url, navigation);
	}

	public static string? Face(IReadOnlyDictionary<string, string>? faces, string suit, string rank, NavigationManager navigation)
	{
		var key = CardDeckSlots.FaceKey(suit, rank);
		if (faces is null || !faces.TryGetValue(key, out var url) || string.IsNullOrWhiteSpace(url))
		{
			return null;
		}

		return Rewrite(url, navigation);
	}

	public static string FaceOrBuiltIn(IReadOnlyDictionary<string, string>? faces, string suit, string rank,
		NavigationManager navigation)
	{
		return Face(faces, suit, rank, navigation) ?? CardDeckSlots.BuiltInFaceUrl(suit, rank);
	}

	public static CardDeckClientMap ToClientMap(SelectedCardDeckImagesDto deck, NavigationManager navigation)
	{
		return new CardDeckClientMap
		{
			BackUrl = Back(deck, navigation),
			Faces = deck.Faces.ToDictionary(
				pair => pair.Key,
				pair => Rewrite(pair.Value, navigation),
				StringComparer.Ordinal)
		};
	}

	public static string? ReadRefitError(IApiResponse response)
	{
		if (response.Error is ApiException exception && exception.TryGetContentAs<ErrorBody>(out var body))
		{
			return body?.Message;
		}

		return null;
	}

	public static string? ReadErrorMessage(string? body)
	{
		if (string.IsNullOrWhiteSpace(body))
		{
			return null;
		}

		try
		{
			using var document = JsonDocument.Parse(body);
			if (document.RootElement.TryGetProperty("message", out var message) &&
			    message.ValueKind == JsonValueKind.String)
			{
				return message.GetString();
			}
		}
		catch (JsonException)
		{
			return null;
		}

		return null;
	}

	public static string Rewrite(string url, NavigationManager navigation)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var imageUri))
		{
			return url;
		}

		if (TryGetLocalImagePath(imageUri, out var localPath))
		{
			return localPath;
		}

		var isLocalHost = imageUri.IsLoopback ||
		                  string.Equals(imageUri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
		if (!isLocalHost)
		{
			return url;
		}

		var currentUri = navigation.ToAbsoluteUri(navigation.Uri);
		if (!string.Equals(currentUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			return url;
		}

		return new UriBuilder(imageUri)
		{
			Scheme = currentUri.Scheme,
			Host = currentUri.Host,
			Port = currentUri.IsDefaultPort ? -1 : currentUri.Port
		}.Uri.ToString();
	}

	private static bool TryGetLocalImagePath(Uri imageUri, out string path)
	{
		path = string.Empty;
		var isLocalHost = imageUri.IsLoopback ||
		                  string.Equals(imageUri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
		if (!isLocalHost)
		{
			return false;
		}

		var segments = imageUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length < 3)
		{
			return false;
		}

		var isBlobPath = segments.Any(segment =>
			                 segment.Equals("devstoreaccount1", StringComparison.OrdinalIgnoreCase) ||
			                 segment.Equals("card-decks", StringComparison.OrdinalIgnoreCase));
		if (!isBlobPath || !Guid.TryParse(segments[^2], out var deckId))
		{
			return false;
		}

		var slotKey = Path.GetFileNameWithoutExtension(segments[^1]);
		if (!CardDeckSlots.IsKnownSlot(slotKey))
		{
			return false;
		}

		path = $"/card-deck-images/{deckId:D}/{slotKey}";
		return true;
	}
}

internal sealed class ErrorBody
{
	[JsonPropertyName("message")]
	public string? Message { get; set; }
}

public sealed class CardDeckClientMap
{
	public string BackUrl { get; set; } = CardDeckSlots.BuiltInBackUrl;

	public Dictionary<string, string> Faces { get; set; } = new(StringComparer.Ordinal);
}
