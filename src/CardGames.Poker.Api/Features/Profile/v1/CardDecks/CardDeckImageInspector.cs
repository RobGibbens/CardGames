using SixLabors.ImageSharp;

namespace CardGames.Poker.Api.Features.Profile.v1.CardDecks;

public static class CardDeckImageInspector
{
	public static bool TryInspect(byte[] content, out int width, out int height, out string contentType,
		out string extension, out string? error)
	{
		width = 0;
		height = 0;
		contentType = string.Empty;
		extension = string.Empty;
		error = null;

		if (content.Length == 0)
		{
			error = "Choose an image file.";
			return false;
		}

		if (content.Length > CardGames.Poker.Api.Contracts.CardDeckSlots.MaxUploadBytes)
		{
			error = "Image must be 5 MB or smaller.";
			return false;
		}

		try
		{
			using var stream = new MemoryStream(content, writable: false);
			var format = Image.DetectFormat(stream);
			if (format is null || !TryMapFormat(format.Name, out contentType, out extension))
			{
				error = "Use a PNG, JPEG, or WebP image.";
				return false;
			}

			stream.Position = 0;
			var info = Image.Identify(stream);
			if (info is null)
			{
				error = "Use a PNG, JPEG, or WebP image.";
				return false;
			}

			width = info.Width;
			height = info.Height;
		}
		catch (UnknownImageFormatException)
		{
			error = "Use a PNG, JPEG, or WebP image.";
			return false;
		}
		catch (InvalidImageContentException)
		{
			error = "Use a PNG, JPEG, or WebP image.";
			return false;
		}

		if (height != CardGames.Poker.Api.Contracts.CardDeckSlots.RequiredHeight ||
		    width < CardGames.Poker.Api.Contracts.CardDeckSlots.MinimumWidth ||
		    width > CardGames.Poker.Api.Contracts.CardDeckSlots.MaximumWidth)
		{
			error = "Image must be 1259 pixels high and between 750 and 920 pixels wide.";
			return false;
		}

		return true;
	}

	private static bool TryMapFormat(string formatName, out string contentType, out string extension)
	{
		switch (formatName.ToUpperInvariant())
		{
			case "PNG":
				contentType = "image/png";
				extension = ".png";
				return true;
			case "JPEG":
			case "JPG":
				contentType = "image/jpeg";
				extension = ".jpg";
				return true;
			case "WEBP":
				contentType = "image/webp";
				extension = ".webp";
				return true;
			default:
				contentType = string.Empty;
				extension = string.Empty;
				return false;
		}
	}
}
