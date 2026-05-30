#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Profile.v1.Queries.GetFavoriteVariants;
using CardGames.Poker.Api.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Profile.v1.Queries.GetFavoriteVariants;

public class GetFavoriteVariantsQueryHandlerTests
{
	[Fact]
	public async Task Handle_WhenPreferencesAreMissing_ReturnsEmptyFavorites()
	{
		await using var context = CreateContext();
		var sut = new GetFavoriteVariantsQueryHandler(
			context,
			CreateCurrentUserService(isAuthenticated: true, userId: "favorite-user"));

		var result = await sut.Handle(new GetFavoriteVariantsQuery(), CancellationToken.None);

		result.FavoriteVariantCodes.Should().BeEmpty();
	}

	[Fact]
	public async Task Handle_WhenStoredFavoriteVariantsJsonIsMalformed_ReturnsEmptyFavorites()
	{
		const string userId = "favorite-json-user";
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			await SeedUserAsync(setupContext, userId, userName: "favorite-json-user", email: "favorite-json@example.com");
			await SeedPreferencesAsync(setupContext, userId, "not valid json");
		}

		await using var context = CreateContext(databaseName, databaseRoot);
		var sut = new GetFavoriteVariantsQueryHandler(
			context,
			CreateCurrentUserService(isAuthenticated: true, userId: userId));

		var result = await sut.Handle(new GetFavoriteVariantsQuery(), CancellationToken.None);

		result.FavoriteVariantCodes.Should().BeEmpty();
	}

	[Fact]
	public async Task Handle_WhenCurrentUserIdIsStale_UsesResolvedLocalUserFavorites()
	{
		const string localUserId = "favorite-query-local-user";
		const string externalUserId = "favorite-query-external-user";
		const string userName = "favorite-query-user";
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			await SeedUserAsync(setupContext, localUserId, userName, email: "favorite-query@example.com");
			await SeedPreferencesAsync(setupContext, localUserId, "[\" holdem \",\"DEALERSCHOICE\",\"HoldEm\"]");
		}

		await using var context = CreateContext(databaseName, databaseRoot);
		var sut = new GetFavoriteVariantsQueryHandler(
			context,
			CreateCurrentUserService(
				isAuthenticated: true,
				userId: externalUserId,
				userName: userName));

		var result = await sut.Handle(new GetFavoriteVariantsQuery(), CancellationToken.None);

		result.FavoriteVariantCodes.Should().Equal(["DEALERSCHOICE", "HOLDEM"]);
	}

	private static CardsDbContext CreateContext()
	{
		return CreateContext(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot());
	}

	private static CardsDbContext CreateContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot)
			.Options;

		return new CardsDbContext(options);
	}

	private static ICurrentUserService CreateCurrentUserService(
		bool isAuthenticated,
		string? userId,
		string? userName = null,
		string? userEmail = null)
	{
		var currentUserService = Substitute.For<ICurrentUserService>();
		currentUserService.IsAuthenticated.Returns(isAuthenticated);
		currentUserService.UserId.Returns(userId);
		currentUserService.UserName.Returns(userName);
		currentUserService.UserEmail.Returns(userEmail);
		return currentUserService;
	}

	private static async Task SeedUserAsync(CardsDbContext context, string userId, string userName, string email)
	{
		context.Users.Add(new ApplicationUser
		{
			Id = userId,
			UserName = userName,
			NormalizedUserName = userName.ToUpperInvariant(),
			Email = email,
			NormalizedEmail = email.ToUpperInvariant()
		});

		await context.SaveChangesAsync();
	}

	private static async Task SeedPreferencesAsync(CardsDbContext context, string userId, string favoriteVariantCodesJson)
	{
		var now = DateTimeOffset.UtcNow;
		context.UserGamePreferences.Add(new UserGamePreferences
		{
			UserId = userId,
			DefaultSmallBlind = 1,
			DefaultBigBlind = 2,
			DefaultAnte = 5,
			DefaultMinimumBet = 10,
			FavoriteVariantCodesJson = favoriteVariantCodesJson,
			CreatedAtUtc = now,
			UpdatedAtUtc = now,
			RowVersion = [1, 2, 3]
		});

		await context.SaveChangesAsync();
	}
}