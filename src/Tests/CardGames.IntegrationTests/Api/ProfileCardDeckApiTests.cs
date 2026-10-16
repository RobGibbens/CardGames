using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CardGames.IntegrationTests.Api;

public class ProfileCardDeckApiTests(CardDeckApiFactory factory)
	: ApiIntegrationTestBase(factory), IClassFixture<CardDeckApiFactory>
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly CardDeckApiFactory _factory = factory;

	[Fact]
	public async Task Upload_RejectsWrongDimensions_AndStoresNothing()
	{
		await SeedUserAsync("deck-user-size");
		SetUser("deck-user-size");
		var deckId = await CreateDeckAsync("Size Check");

		var response = await UploadAsync(deckId, false, "spades", "ace", Png(100, 100));

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var detail = await GetDeckAsync(deckId);
		detail.Slots.Should().OnlyContain(slot => slot.ImageUrl == null);
		_factory.Storage.Names.Should().BeEmpty();
	}

	[Fact]
	public async Task Upload_StoresValidImage_AndReplaceKeepsOneBlob()
	{
		await SeedUserAsync("deck-user-replace");
		SetUser("deck-user-replace");
		var deckId = await CreateDeckAsync("Replace");

		var first = await UploadAsync(deckId, false, "hearts", "ace", Png(800, CardDeckSlots.RequiredHeight));
		first.StatusCode.Should().Be(HttpStatusCode.OK);

		var second = await UploadAsync(deckId, false, "hearts", "ace", Png(900, CardDeckSlots.RequiredHeight));
		second.StatusCode.Should().Be(HttpStatusCode.OK);

		var detail = await GetDeckAsync(deckId);
		detail.UploadedFaceCount.Should().Be(1);
		detail.Slots.Single(slot => slot.SlotKey == "hearts-ace").ImageUrl.Should().NotBeNullOrWhiteSpace();
		_factory.Storage.Names.Should().ContainSingle(name => name.Contains("hearts-ace", StringComparison.Ordinal));
	}

	[Fact]
	public async Task PartialDeck_CanBeLoadedLater_AndAnotherSlotCanBeAdded()
	{
		await SeedUserAsync("deck-user-partial");
		SetUser("deck-user-partial");
		var deckId = await CreateDeckAsync("Cabin");
		(await UploadAsync(deckId, false, "clubs", "ten", Png(760, CardDeckSlots.RequiredHeight))).StatusCode
			.Should().Be(HttpStatusCode.OK);

		var listed = await GetAsync<CardDeckListDto>("/api/v1/profile/decks");
		var partial = listed!.Decks.Single(deck => deck.Id == deckId);
		partial.IsComplete.Should().BeFalse();
		partial.IsSelected.Should().BeFalse();
		partial.UploadedFaceCount.Should().Be(1);

		var reopened = await GetDeckAsync(deckId);
		reopened.Slots.Single(slot => slot.SlotKey == "clubs-ten").ImageUrl.Should().NotBeNullOrWhiteSpace();

		(await UploadAsync(deckId, true, null, null, Png(810, CardDeckSlots.RequiredHeight))).StatusCode
			.Should().Be(HttpStatusCode.OK);
		(await GetDeckAsync(deckId)).HasBack.Should().BeTrue();
	}

	[Fact]
	public async Task Select_RejectsIncompleteDeck_AndAcceptsFinishedDeck()
	{
		await SeedUserAsync("deck-user-select");
		SetUser("deck-user-select");
		var deckId = await CreateDeckAsync("Finished");

		var tooSoon = await SelectAsync(deckId);
		tooSoon.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		(await UploadAsync(deckId, true, null, null, Png(800, CardDeckSlots.RequiredHeight))).EnsureSuccessStatusCode();
		foreach (var (suit, rank) in CardDeckSlots.Faces())
		{
			(await UploadAsync(deckId, false, suit, rank, Png(820, CardDeckSlots.RequiredHeight)))
				.EnsureSuccessStatusCode();
		}

		var selected = await SelectAsync(deckId);
		selected.StatusCode.Should().Be(HttpStatusCode.OK);
		var images = await GetAsync<SelectedCardDeckImagesDto>("/api/v1/profile/decks/selected-images");
		images!.DeckId.Should().Be(deckId);
		images.Faces.Should().HaveCount(CardDeckSlots.RequiredFaceCount);
		images.BackUrl.Should().Contain(deckId.ToString());
	}

	[Fact]
	public async Task UploadedImage_IsServedToTheOwner()
	{
		await SeedUserAsync("deck-user-image");
		await SeedUserAsync("deck-user-image-other");
		SetUser("deck-user-image");
		var deckId = await CreateDeckAsync("Images");
		var png = Png(800, CardDeckSlots.RequiredHeight);
		var blobName = $"deck-user-image/{deckId}/{CardDeckSlots.BackSlotKey}.png";
		await _factory.Storage.UploadAsync(blobName, new MemoryStream(png), "image/png", CancellationToken.None);
		DbContext.UserCardDeckImages.Add(new UserCardDeckImage
		{
			Id = Guid.NewGuid(),
			DeckId = deckId,
			SlotKey = CardDeckSlots.BackSlotKey,
			IsBack = true,
			BlobName = blobName,
			ContentType = "image/png",
			Width = 800,
			Height = CardDeckSlots.RequiredHeight
		});
		await DbContext.SaveChangesAsync();

		var response = await Client.GetAsync($"/api/v1/profile/decks/{deckId}/images/{CardDeckSlots.BackSlotKey}");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
		(await response.Content.ReadAsByteArrayAsync()).Should().Equal(png);

		SetUser("deck-user-image-other");
		(await Client.GetAsync($"/api/v1/profile/decks/{deckId}/images/{CardDeckSlots.BackSlotKey}")).StatusCode
			.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task OtherUser_CannotReadUploadOrSelectDeck()
	{
		await SeedUserAsync("deck-user-owner");
		await SeedUserAsync("deck-user-other");
		SetUser("deck-user-owner");
		var deckId = await CreateDeckAsync("Private");

		SetUser("deck-user-other");
		(await Client.GetAsync($"/api/v1/profile/decks/{deckId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await UploadAsync(deckId, true, null, null, Png(800, CardDeckSlots.RequiredHeight))).StatusCode
			.Should().Be(HttpStatusCode.NotFound);
		(await SelectAsync(deckId)).StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task List_OffersBothBuiltInDecks_WithFridayNightPokerSelected()
	{
		await SeedUserAsync("deck-user-builtins");
		SetUser("deck-user-builtins");

		var listed = await GetAsync<CardDeckListDto>("/api/v1/profile/decks");
		listed!.Decks.Where(deck => deck.IsBuiltIn).Select(deck => deck.Name)
			.Should().Equal(CardDeckSlots.FridayNightPoker.Name, CardDeckSlots.Basic.Name);
		listed.Decks.Single(deck => deck.IsSelected).Name.Should().Be(CardDeckSlots.FridayNightPoker.Name);

		var images = await GetAsync<SelectedCardDeckImagesDto>("/api/v1/profile/decks/selected-images");
		images!.Name.Should().Be(CardDeckSlots.FridayNightPoker.Name);
		images.BackUrl.Should().Be(CardDeckSlots.FridayNightPoker.BackUrl);
		images.Faces["spades-ace"].Should().StartWith(CardDeckSlots.FridayNightPoker.ImageBase);
	}

	[Fact]
	public async Task Select_BasicBuiltIn_UsesBasicImages()
	{
		await SeedUserAsync("deck-user-basic");
		SetUser("deck-user-basic");

		var selected = await SelectAsync(CardDeckSlots.Basic.Id!.Value);
		selected.StatusCode.Should().Be(HttpStatusCode.OK);
		var list = await selected.Content.ReadFromJsonAsync<CardDeckListDto>(JsonOptions);
		list!.Decks.Single(deck => deck.IsSelected).Name.Should().Be(CardDeckSlots.Basic.Name);

		var images = await GetAsync<SelectedCardDeckImagesDto>("/api/v1/profile/decks/selected-images");
		images!.DeckId.Should().Be(CardDeckSlots.Basic.Id);
		images.BackUrl.Should().Be(CardDeckSlots.Basic.BackUrl);
		images.Faces["hearts-king"].Should().StartWith(CardDeckSlots.Basic.ImageBase);

		(await SelectAsync(null)).EnsureSuccessStatusCode();
		var restored = await GetAsync<SelectedCardDeckImagesDto>("/api/v1/profile/decks/selected-images");
		restored!.Name.Should().Be(CardDeckSlots.FridayNightPoker.Name);
		restored.Faces["hearts-king"].Should().StartWith(CardDeckSlots.FridayNightPoker.ImageBase);
	}

	[Fact]
	public async Task Delete_SelectedDeck_ReturnsBuiltInImages()
	{
		await SeedUserAsync("deck-user-delete");
		SetUser("deck-user-delete");
		var deckId = await CreateDeckAsync("Temporary");
		(await UploadAsync(deckId, true, null, null, Png(800, CardDeckSlots.RequiredHeight))).EnsureSuccessStatusCode();
		foreach (var (suit, rank) in CardDeckSlots.Faces())
		{
			(await UploadAsync(deckId, false, suit, rank, Png(830, CardDeckSlots.RequiredHeight)))
				.EnsureSuccessStatusCode();
		}

		(await SelectAsync(deckId)).EnsureSuccessStatusCode();
		(await Client.DeleteAsync($"/api/v1/profile/decks/{deckId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

		var images = await GetAsync<SelectedCardDeckImagesDto>("/api/v1/profile/decks/selected-images");
		images!.DeckId.Should().BeNull();
		images.Name.Should().Be(CardDeckSlots.BuiltInName);
		images.BackUrl.Should().Be(CardDeckSlots.BuiltInBackUrl);
		_factory.Storage.Names.Should().BeEmpty();
	}

	private async Task SeedUserAsync(string userId)
	{
		DbContext.Users.Add(new ApplicationUser
		{
			Id = userId,
			UserName = userId,
			NormalizedUserName = userId.ToUpperInvariant(),
			Email = $"{userId}@example.com",
			NormalizedEmail = $"{userId}@example.com".ToUpperInvariant()
		});
		await DbContext.SaveChangesAsync();
	}

	private async Task<Guid> CreateDeckAsync(string name)
	{
		var response = await PostAsync("/api/v1/profile/decks", new CreateCardDeckRequest { Name = name });
		response.EnsureSuccessStatusCode();
		var deck = await response.Content.ReadFromJsonAsync<CardDeckDetailDto>(JsonOptions);
		return deck!.Id;
	}

	private async Task<CardDeckDetailDto> GetDeckAsync(Guid deckId)
	{
		return (await GetAsync<CardDeckDetailDto>($"/api/v1/profile/decks/{deckId}"))!;
	}

	private Task<HttpResponseMessage> SelectAsync(Guid? deckId)
	{
		return Client.PutAsJsonAsync("/api/v1/profile/decks/selected", new SelectCardDeckRequest { DeckId = deckId },
			JsonOptions);
	}

	private Task<HttpResponseMessage> UploadAsync(Guid deckId, bool isBack, string? suit, string? rank, byte[] png)
	{
		var content = new MultipartFormDataContent();
		var file = new ByteArrayContent(png);
		file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
		content.Add(file, "file", "card.png");
		content.Add(new StringContent(isBack ? "true" : "false"), "isBack");
		if (suit is not null)
		{
			content.Add(new StringContent(suit), "suit");
		}

		if (rank is not null)
		{
			content.Add(new StringContent(rank), "rank");
		}

		return Client.PostAsync($"/api/v1/profile/decks/{deckId}/images", content);
	}

	private static byte[] Png(int width, int height)
	{
		using var image = new Image<Rgba32>(width, height);
		using var stream = new MemoryStream();
		image.SaveAsPng(stream);
		return stream.ToArray();
	}

	private void SetUser(string userId)
	{
		Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
		Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
	}
}
