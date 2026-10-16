using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Web.Services;

public static class CardDeckImageProxy
{
	public const string HttpClientName = "card-deck-images";

	public static void Map(WebApplication app)
	{
		app.MapGet("/card-deck-images/{deckId:guid}/{slotKey}", async (Guid deckId, string slotKey,
				IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
			{
				if (!CardDeckSlots.IsKnownSlot(slotKey))
				{
					return Results.NotFound();
				}

				var client = httpClientFactory.CreateClient(HttpClientName);
				using var response = await client.GetAsync(
					$"/api/v1/profile/decks/{deckId:D}/images/{Uri.EscapeDataString(slotKey)}",
					HttpCompletionOption.ResponseHeadersRead,
					cancellationToken);
				if (!response.IsSuccessStatusCode)
				{
					return Results.StatusCode((int)response.StatusCode);
				}

				var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
				var contentType = response.Content.Headers.ContentType?.MediaType;
				return Results.File(content, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
			})
			.RequireAuthorization();
	}
}
