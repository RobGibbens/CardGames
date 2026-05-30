#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Profile.v1.Queries.GetGamePreferences;
using CardGames.Poker.Api.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Profile.v1.Queries.GetGamePreferences;

public class GetGamePreferencesQueryHandlerTests
{
	[Fact]
	public async Task Handle_WhenPreferencesAreMissing_ReturnsDefaults()
	{
		await using var context = CreateContext();
		var sut = new GetGamePreferencesQueryHandler(
			context,
			CreateCurrentUserService(isAuthenticated: true, userId: "profile-user"));

		var result = await sut.Handle(new GetGamePreferencesQuery(), CancellationToken.None);

		result.DefaultSmallBlind.Should().Be(1);
		result.DefaultBigBlind.Should().Be(2);
		result.DefaultAnte.Should().Be(5);
		result.DefaultMinimumBet.Should().Be(10);
	}

	[Fact]
	public async Task Handle_WhenCurrentUserIdIsStale_UsesResolvedLocalUserPreferences()
	{
		const string localUserId = "query-local-user";
		const string externalUserId = "query-external-user";
		const string email = "query-user@example.com";
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			await SeedUserAsync(setupContext, localUserId, userName: "query-user", email);
			await SeedPreferencesAsync(setupContext, localUserId, 10, 20, 5, 20);
		}

		await using var context = CreateContext(databaseName, databaseRoot);
		var sut = new GetGamePreferencesQueryHandler(
			context,
			CreateCurrentUserService(
				isAuthenticated: true,
				userId: externalUserId,
				userEmail: email));

		var result = await sut.Handle(new GetGamePreferencesQuery(), CancellationToken.None);

		result.DefaultSmallBlind.Should().Be(10);
		result.DefaultBigBlind.Should().Be(20);
		result.DefaultAnte.Should().Be(5);
		result.DefaultMinimumBet.Should().Be(20);
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

	private static async Task SeedPreferencesAsync(
		CardsDbContext context,
		string userId,
		int defaultSmallBlind,
		int defaultBigBlind,
		int defaultAnte,
		int defaultMinimumBet)
	{
		var now = DateTimeOffset.UtcNow;
		context.UserGamePreferences.Add(new UserGamePreferences
		{
			UserId = userId,
			DefaultSmallBlind = defaultSmallBlind,
			DefaultBigBlind = defaultBigBlind,
			DefaultAnte = defaultAnte,
			DefaultMinimumBet = defaultMinimumBet,
			CreatedAtUtc = now,
			UpdatedAtUtc = now,
			RowVersion = [1, 2, 3]
		});

		await context.SaveChangesAsync();
	}
}