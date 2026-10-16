using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.CardDecks;

public sealed record UploadCardDeckImageCommand(
	Guid DeckId,
	bool IsBack,
	string? Suit,
	string? Rank,
	byte[] Content) : IRequest<OneOf<CardDeckDetailDto, CardDeckError>>;

public sealed class UploadCardDeckImageCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUser,
	ICardDeckStorageService storage)
	: IRequestHandler<UploadCardDeckImageCommand, OneOf<CardDeckDetailDto, CardDeckError>>
{
	public async Task<OneOf<CardDeckDetailDto, CardDeckError>> Handle(UploadCardDeckImageCommand request,
		CancellationToken cancellationToken)
	{
		var userId = await CardDeckSupport.GetUserIdAsync(context, currentUser, cancellationToken);
		if (userId is null)
		{
			return new CardDeckError(CardDeckErrorCode.Unauthorized, "User is not authenticated.");
		}

		string slotKey;
		string? suit = null;
		string? rank = null;
		if (request.IsBack)
		{
			slotKey = CardDeckSlots.BackSlotKey;
		}
		else if (!CardDeckSlots.TryGetFace(request.Suit, request.Rank, out slotKey))
		{
			return new CardDeckError(CardDeckErrorCode.InvalidImage, "Choose a suit and value for this card.");
		}
		else
		{
			suit = request.Suit!.Trim().ToLowerInvariant();
			rank = request.Rank!.Trim().ToLowerInvariant();
		}

		if (!CardDeckImageInspector.TryInspect(request.Content, out var width, out var height, out var contentType,
			    out var extension, out var imageError))
		{
			return new CardDeckError(CardDeckErrorCode.InvalidImage, imageError!);
		}

		var deckExists = await context.UserCardDecks.AnyAsync(
			candidate => candidate.Id == request.DeckId && candidate.UserId == userId,
			cancellationToken);
		if (!deckExists)
		{
			return new CardDeckError(CardDeckErrorCode.NotFound, "Deck was not found.");
		}

		var blobName = $"{userId}/{request.DeckId}/{slotKey}{extension}";
		var previousBlobName = await context.UserCardDeckImages
			.Where(image => image.DeckId == request.DeckId && image.SlotKey == slotKey)
			.Select(image => image.BlobName)
			.FirstOrDefaultAsync(cancellationToken);

		await using var stream = new MemoryStream(request.Content, writable: false);
		await storage.UploadAsync(blobName, stream, contentType, cancellationToken);

		if (previousBlobName is not null
		    && !string.Equals(previousBlobName, blobName, StringComparison.Ordinal))
		{
			await storage.DeleteAsync(previousBlobName, cancellationToken);
		}

		var deck = await context.UserCardDecks
			.Include(candidate => candidate.Images)
			.FirstOrDefaultAsync(candidate => candidate.Id == request.DeckId && candidate.UserId == userId,
				cancellationToken);
		if (deck is null)
		{
			return new CardDeckError(CardDeckErrorCode.NotFound, "Deck was not found.");
		}

		var existing = deck.Images.FirstOrDefault(image => image.SlotKey == slotKey);
		if (existing is null)
		{
			// Guid keys are store-generated. Adding this image through the deck collection makes EF
			// treat the explicit key as an existing row and issue an UPDATE that affects nothing.
			context.UserCardDeckImages.Add(new UserCardDeckImage
			{
				Id = Guid.NewGuid(),
				DeckId = deck.Id,
				SlotKey = slotKey,
				Suit = suit,
				Rank = rank,
				IsBack = request.IsBack,
				BlobName = blobName,
				ContentType = contentType,
				Width = width,
				Height = height
			});
		}
		else
		{
			existing.BlobName = blobName;
			existing.ContentType = contentType;
			existing.Width = width;
			existing.Height = height;
			existing.Suit = suit;
			existing.Rank = rank;
			existing.IsBack = request.IsBack;
		}

		var updatedAtUtc = DateTimeOffset.UtcNow;
		await context.SaveChangesAsync(cancellationToken);
		await context.UserCardDecks
			.Where(candidate => candidate.Id == deck.Id && candidate.UserId == userId)
			.ExecuteUpdateAsync(
				setters => setters.SetProperty(candidate => candidate.UpdatedAtUtc, updatedAtUtc),
				cancellationToken);
		deck.UpdatedAtUtc = updatedAtUtc;
		return CardDeckSupport.ToDetail(deck, storage);
	}
}
