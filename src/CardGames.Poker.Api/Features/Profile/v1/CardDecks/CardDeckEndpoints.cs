using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Profile.v1.CardDecks;

public static class CardDeckEndpoints
{
	public static RouteGroupBuilder MapCardDecks(this RouteGroupBuilder group)
	{
		group.MapGet("decks", async (IMediator mediator, CancellationToken cancellationToken) =>
			{
				var decks = await mediator.Send(new ListCardDecksQuery(), cancellationToken);
				return Results.Ok(decks);
			})
			.WithName("ListCardDecks")
			.WithSummary("List card decks")
			.WithDescription("Lists the built-in deck and every custom deck for the authenticated player, including partial decks.")
			.Produces<CardGames.Poker.Api.Contracts.CardDeckListDto>()
			.RequireAuthorization();

		group.MapGet("decks/selected-images", async (IMediator mediator, CancellationToken cancellationToken) =>
			{
				var images = await mediator.Send(new GetSelectedCardDeckImagesQuery(), cancellationToken);
				return Results.Ok(images);
			})
			.WithName("GetSelectedCardDeckImages")
			.WithSummary("Get selected card deck images")
			.WithDescription("Returns the image map for the deck the authenticated player uses in games.")
			.Produces<CardGames.Poker.Api.Contracts.SelectedCardDeckImagesDto>()
			.RequireAuthorization();

		group.MapPut("decks/selected", async (CardGames.Poker.Api.Contracts.SelectCardDeckRequest request, IMediator mediator,
				CancellationToken cancellationToken) =>
			{
				var result = await mediator.Send(new SelectCardDeckCommand(request.DeckId), cancellationToken);
				return result.Match<IResult>(
					success => Results.Ok(success),
					error => ToError(error));
			})
			.WithName("SelectCardDeck")
			.WithSummary("Select card deck")
			.WithDescription("Sets the complete deck used for the authenticated player. Incomplete decks are rejected.")
			.Produces<CardGames.Poker.Api.Contracts.CardDeckListDto>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		group.MapPost("decks", async (CardGames.Poker.Api.Contracts.CreateCardDeckRequest request, IMediator mediator,
				CancellationToken cancellationToken) =>
			{
				var result = await mediator.Send(new CreateCardDeckCommand(request.Name), cancellationToken);
				return result.Match<IResult>(
					success => Results.Ok(success),
					error => ToError(error));
			})
			.WithName("CreateCardDeck")
			.WithSummary("Create card deck")
			.WithDescription("Creates an empty custom deck that can be filled in over multiple visits.")
			.Produces<CardGames.Poker.Api.Contracts.CardDeckDetailDto>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		group.MapGet("decks/{deckId:guid}", async (Guid deckId, IMediator mediator, CancellationToken cancellationToken) =>
			{
				var result = await mediator.Send(new GetCardDeckQuery(deckId), cancellationToken);
				return result.Match<IResult>(
					success => Results.Ok(success),
					error => ToError(error));
			})
			.WithName("GetCardDeck")
			.WithSummary("Get card deck")
			.WithDescription("Returns a custom deck and its uploaded slots for the owner.")
			.Produces<CardGames.Poker.Api.Contracts.CardDeckDetailDto>()
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireAuthorization();

		group.MapPut("decks/{deckId:guid}", async (Guid deckId, CardGames.Poker.Api.Contracts.RenameCardDeckRequest request,
				IMediator mediator, CancellationToken cancellationToken) =>
			{
				var result = await mediator.Send(new RenameCardDeckCommand(deckId, request.Name), cancellationToken);
				return result.Match<IResult>(
					success => Results.Ok(success),
					error => ToError(error));
			})
			.WithName("RenameCardDeck")
			.WithSummary("Rename card deck")
			.WithDescription("Renames a custom deck owned by the authenticated player.")
			.Produces<CardGames.Poker.Api.Contracts.CardDeckDetailDto>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		group.MapDelete("decks/{deckId:guid}", async (Guid deckId, IMediator mediator, CancellationToken cancellationToken) =>
			{
				var result = await mediator.Send(new DeleteCardDeckCommand(deckId), cancellationToken);
				return result.Match<IResult>(
					_ => Results.NoContent(),
					error => ToError(error));
			})
			.WithName("DeleteCardDeck")
			.WithSummary("Delete card deck")
			.WithDescription("Deletes a custom deck and its images. The built-in deck is used if this was the selected deck.")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireAuthorization();

		group.MapGet("decks/{deckId:guid}/images/{slotKey}", async (Guid deckId, string slotKey, CardsDbContext context,
				ICurrentUserService currentUser, ICardDeckStorageService storage, CancellationToken cancellationToken) =>
			{
				if (!CardGames.Poker.Api.Contracts.CardDeckSlots.IsKnownSlot(slotKey))
				{
					return Results.NotFound();
				}

				var userId = await CardDeckSupport.GetUserIdAsync(context, currentUser, cancellationToken);
				if (userId is null)
				{
					return Results.Unauthorized();
				}

				var image = await context.UserCardDeckImages.AsNoTracking()
					.Where(candidate => candidate.DeckId == deckId && candidate.SlotKey == slotKey &&
					                    candidate.Deck.UserId == userId)
					.Select(candidate => new { candidate.BlobName, candidate.ContentType })
					.FirstOrDefaultAsync(cancellationToken);
				if (image is null)
				{
					return Results.NotFound();
				}

				var content = await storage.OpenReadAsync(image.BlobName, cancellationToken);
				if (content is null)
				{
					return Results.NotFound();
				}

				return Results.File(content, string.IsNullOrWhiteSpace(image.ContentType)
					? "application/octet-stream"
					: image.ContentType);
			})
			.WithName("GetCardDeckImage")
			.WithSummary("Get card deck image")
			.WithDescription("Returns one uploaded card image for a deck owned by the authenticated player.")
			.Produces(StatusCodes.Status200OK, contentType: "image/png")
			.Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
			.Produces(StatusCodes.Status200OK, contentType: "image/webp")
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireAuthorization();

		group.MapPost("decks/{deckId:guid}/images", async (Guid deckId, HttpRequest request, IMediator mediator,
				CancellationToken cancellationToken) =>
			{
				if (!request.HasFormContentType)
				{
					return Results.BadRequest(new { message = "Upload the image as form data." });
				}

				var form = await request.ReadFormAsync(cancellationToken);
				var file = form.Files.GetFile("file");
				if (file is null || file.Length <= 0)
				{
					return Results.BadRequest(new { message = "Choose an image file." });
				}

				if (file.Length > CardGames.Poker.Api.Contracts.CardDeckSlots.MaxUploadBytes)
				{
					return Results.BadRequest(new { message = "Image must be 5 MB or smaller." });
				}

				var isBack = bool.TryParse(form["isBack"], out var parsedBack) && parsedBack;
				await using var stream = file.OpenReadStream();
				using var buffer = new MemoryStream();
				await stream.CopyToAsync(buffer, cancellationToken);

				var result = await mediator.Send(new UploadCardDeckImageCommand(
					deckId,
					isBack,
					form["suit"],
					form["rank"],
					buffer.ToArray()), cancellationToken);

				return result.Match<IResult>(
					success => Results.Ok(success),
					error => ToError(error));
			})
			.WithName("UploadCardDeckImage")
			.WithSummary("Upload card deck image")
			.WithDescription("Stores one back or face image on a custom deck. A partial deck can be continued later.")
			.DisableAntiforgery()
			.Accepts<IFormFile>("multipart/form-data")
			.Produces<CardGames.Poker.Api.Contracts.CardDeckDetailDto>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		return group;
	}

	private static IResult ToError(CardDeckError error)
	{
		return error.Code switch
		{
			CardDeckErrorCode.Unauthorized => Results.Unauthorized(),
			CardDeckErrorCode.NotFound => Results.NotFound(new { message = error.Message }),
			CardDeckErrorCode.Incomplete => Results.BadRequest(new { message = error.Message }),
			CardDeckErrorCode.DuplicateName => Results.Conflict(new { message = error.Message }),
			_ => Results.BadRequest(new { message = error.Message })
		};
	}
}
