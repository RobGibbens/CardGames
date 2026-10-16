using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Profile.v1.CardDecks;

public enum CardDeckErrorCode
{
	Unauthorized,
	NotFound,
	InvalidName,
	DuplicateName,
	LimitReached,
	InvalidImage,
	Incomplete
}

public sealed record CardDeckError(CardDeckErrorCode Code, string Message);

public static class CardDeckSupport
{
	public static async Task<string?> GetUserIdAsync(CardsDbContext context, ICurrentUserService currentUser,
		CancellationToken cancellationToken)
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
		{
			return null;
		}

		var userId = currentUser.UserId;
		var exists = await context.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken);
		if (exists)
		{
			return userId;
		}

		if (!string.IsNullOrWhiteSpace(currentUser.UserEmail))
		{
			var normalizedEmail = currentUser.UserEmail.ToUpperInvariant();
			var byEmail = await context.Users.AsNoTracking()
				.Where(user => user.NormalizedEmail == normalizedEmail || user.Email == currentUser.UserEmail)
				.Select(user => user.Id)
				.FirstOrDefaultAsync(cancellationToken);
			if (!string.IsNullOrWhiteSpace(byEmail))
			{
				return byEmail;
			}
		}

		if (!string.IsNullOrWhiteSpace(currentUser.UserName))
		{
			var normalizedUserName = currentUser.UserName.ToUpperInvariant();
			var byUserName = await context.Users.AsNoTracking()
				.Where(user => user.NormalizedUserName == normalizedUserName || user.UserName == currentUser.UserName)
				.Select(user => user.Id)
				.FirstOrDefaultAsync(cancellationToken);
			if (!string.IsNullOrWhiteSpace(byUserName))
			{
				return byUserName;
			}
		}

		return userId;
	}

	public static bool TryNormalizeName(string? name, out string normalized, out string? error)
	{
		normalized = name?.Trim() ?? string.Empty;
		if (normalized.Length == 0)
		{
			error = "Enter a deck name.";
			return false;
		}

		if (normalized.Length > CardDeckSlots.MaxNameLength)
		{
			error = $"Deck name must be {CardDeckSlots.MaxNameLength} characters or fewer.";
			return false;
		}

		if (string.Equals(normalized, CardDeckSlots.BuiltInName, StringComparison.OrdinalIgnoreCase))
		{
			error = "That name is reserved for the built-in deck.";
			return false;
		}

		error = null;
		return true;
	}

	public static bool IsComplete(IEnumerable<UserCardDeckImage> images)
	{
		var list = images as ICollection<UserCardDeckImage> ?? images.ToList();
		var hasBack = list.Any(image => image.IsBack);
		var faces = list.Count(image => !image.IsBack && CardDeckSlots.TryGetFace(image.Suit, image.Rank, out _));
		return hasBack && faces == CardDeckSlots.RequiredFaceCount;
	}

	public static int FaceCount(IEnumerable<UserCardDeckImage> images) =>
		images.Count(image => !image.IsBack && CardDeckSlots.TryGetFace(image.Suit, image.Rank, out _));

	public static bool HasBack(IEnumerable<UserCardDeckImage> images) => images.Any(image => image.IsBack);

	public static CardDeckSummaryDto BuiltInSummary(bool isSelected) => new()
	{
		Id = null,
		Name = CardDeckSlots.BuiltInName,
		IsBuiltIn = true,
		IsComplete = true,
		IsSelected = isSelected,
		UploadedFaceCount = CardDeckSlots.RequiredFaceCount,
		HasBack = true
	};

	public static CardDeckSummaryDto ToSummary(UserCardDeck deck, bool isSelected)
	{
		var images = deck.Images ?? [];
		return new CardDeckSummaryDto
		{
			Id = deck.Id,
			Name = deck.Name,
			IsBuiltIn = false,
			IsComplete = IsComplete(images),
			IsSelected = isSelected,
			UploadedFaceCount = FaceCount(images),
			HasBack = HasBack(images)
		};
	}

	public static CardDeckDetailDto ToDetail(UserCardDeck deck, ICardDeckStorageService storage)
	{
		var images = deck.Images ?? [];
		var bySlot = images.ToDictionary(image => image.SlotKey, StringComparer.Ordinal);
		var slots = new List<CardDeckImageSlotDto>
		{
			ToSlot(CardDeckSlots.BackSlotKey, null, null, true, bySlot, storage)
		};

		foreach (var (suit, rank) in CardDeckSlots.Faces())
		{
			slots.Add(ToSlot(CardDeckSlots.FaceKey(suit, rank), suit, rank, false, bySlot, storage));
		}

		return new CardDeckDetailDto
		{
			Id = deck.Id,
			Name = deck.Name,
			IsComplete = IsComplete(images),
			UploadedFaceCount = FaceCount(images),
			HasBack = HasBack(images),
			Slots = slots
		};
	}

	public static SelectedCardDeckImagesDto BuiltInImages() => new()
	{
		DeckId = null,
		Name = CardDeckSlots.BuiltInName,
		BackUrl = CardDeckSlots.BuiltInBackUrl,
		Faces = CardDeckSlots.Faces().ToDictionary(
			face => CardDeckSlots.FaceKey(face.Suit, face.Rank),
			face => CardDeckSlots.BuiltInFaceUrl(face.Suit, face.Rank),
			StringComparer.Ordinal)
	};

	public static SelectedCardDeckImagesDto ToSelectedImages(UserCardDeck deck, ICardDeckStorageService storage)
	{
		var images = deck.Images ?? [];
		var back = images.FirstOrDefault(image => image.IsBack);
		var faces = CardDeckSlots.Faces().ToDictionary(
			face => CardDeckSlots.FaceKey(face.Suit, face.Rank),
			face =>
			{
				var match = images.FirstOrDefault(image =>
					string.Equals(image.Suit, face.Suit, StringComparison.Ordinal) &&
					string.Equals(image.Rank, face.Rank, StringComparison.Ordinal));
				return match is null ? CardDeckSlots.BuiltInFaceUrl(face.Suit, face.Rank) : storage.GetUrl(match.BlobName);
			},
			StringComparer.Ordinal);

		return new SelectedCardDeckImagesDto
		{
			DeckId = deck.Id,
			Name = deck.Name,
			BackUrl = back is null ? CardDeckSlots.BuiltInBackUrl : storage.GetUrl(back.BlobName),
			Faces = faces
		};
	}

	private static CardDeckImageSlotDto ToSlot(string slotKey, string? suit, string? rank, bool isBack,
		IReadOnlyDictionary<string, UserCardDeckImage> bySlot, ICardDeckStorageService storage)
	{
		bySlot.TryGetValue(slotKey, out var image);
		return new CardDeckImageSlotDto
		{
			SlotKey = slotKey,
			Suit = suit,
			Rank = rank,
			IsBack = isBack,
			ImageUrl = image is null ? null : storage.GetUrl(image.BlobName)
		};
	}
}
