using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Profile.v1.CardDecks;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Testcontainers.MsSql;

namespace CardGames.IntegrationTests.Api;

/// <summary>
///     Card image inserts against SQL Server. The in-memory provider does not check that an update
///     affected a row, so a new image tracked as Modified succeeds there and fails here.
/// </summary>
public class UploadCardDeckImageSqlTests : IAsyncLifetime
{
	private MsSqlContainer _sql = null!;

	public async Task InitializeAsync()
	{
		_sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
		await _sql.StartAsync();
	}

	public async Task DisposeAsync()
	{
		await _sql.DisposeAsync();
	}

	[Fact]
	public async Task UploadBack_InsertsTheImage()
	{
		var userId = "deck-sql-user";
		Guid deckId;
		await using (var setup = CreateContext())
		{
			await setup.Database.EnsureCreatedAsync();
			setup.Users.Add(new ApplicationUser
			{
				Id = userId,
				UserName = userId,
				NormalizedUserName = userId.ToUpperInvariant(),
				Email = $"{userId}@example.com",
				NormalizedEmail = $"{userId}@example.com".ToUpperInvariant()
			});
			var now = DateTimeOffset.UtcNow;
			var deck = new UserCardDeck
			{
				Id = Guid.NewGuid(),
				UserId = userId,
				Name = "Cabin",
				CreatedAtUtc = now,
				UpdatedAtUtc = now
			};
			setup.UserCardDecks.Add(deck);
			await setup.SaveChangesAsync();
			deckId = deck.Id;
		}

		await using var context = CreateContext();
		var handler = new UploadCardDeckImageCommandHandler(context, new FixedUser(userId), new MemoryCardDeckStorage());
		var result = await handler.Handle(
			new UploadCardDeckImageCommand(deckId, true, null, null, Png(800, CardDeckSlots.RequiredHeight)),
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.HasBack.Should().BeTrue();
		(await context.UserCardDeckImages.CountAsync(image => image.DeckId == deckId && image.IsBack)).Should().Be(1);
	}

	private CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseSqlServer(_sql.GetConnectionString())
			.Options;
		return new CardsDbContext(options);
	}

	private static byte[] Png(int width, int height)
	{
		using var image = new Image<Rgba32>(width, height);
		using var stream = new MemoryStream();
		image.SaveAsPng(stream);
		return stream.ToArray();
	}

	private sealed class FixedUser(string userId) : ICurrentUserService
	{
		public string? UserId { get; } = userId;
		public string? UserName => UserId;
		public string? UserEmail => $"{UserId}@example.com";
		public string? SignalRUserId => UserId;
		public bool IsAuthenticated => true;
	}
}
