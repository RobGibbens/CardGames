#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Profile.v1.Commands.UpdateGamePreferences;
using CardGames.Poker.Api.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Profile.v1.Commands.UpdateGamePreferences;

public class UpdateGamePreferencesCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenUserIsUnauthenticated_ReturnsUnauthorized()
	{
		await using var context = CreateContext();
		var sut = new UpdateGamePreferencesCommandHandler(
			context,
			CreateCurrentUserService(isAuthenticated: false, userId: null));

		var result = await sut.Handle(new UpdateGamePreferencesCommand(1, 2, 5, 10), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(UpdateGamePreferencesErrorCode.Unauthorized);
	}

	[Fact]
	public async Task Handle_WhenPreferenceValueIsNegative_ReturnsInvalidPreferences()
	{
		await using var context = CreateContext();
		var sut = new UpdateGamePreferencesCommandHandler(
			context,
			CreateCurrentUserService(isAuthenticated: true, userId: "profile-user"));

		var result = await sut.Handle(new UpdateGamePreferencesCommand(-1, 2, 5, 10), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(UpdateGamePreferencesErrorCode.InvalidPreferences);
		result.AsT1.Message.Should().Contain("greater than or equal to 0");
	}

	[Fact]
	public async Task Handle_WhenBigBlindIsSmallerThanSmallBlind_ReturnsInvalidPreferences()
	{
		await using var context = CreateContext();
		var sut = new UpdateGamePreferencesCommandHandler(
			context,
			CreateCurrentUserService(isAuthenticated: true, userId: "profile-user"));

		var result = await sut.Handle(new UpdateGamePreferencesCommand(20, 10, 5, 20), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(UpdateGamePreferencesErrorCode.InvalidPreferences);
		result.AsT1.Message.Should().Contain("Big blind must be greater than or equal to small blind");
	}

	[Fact]
	public async Task Handle_WhenInitialSaveFailsForStaleUserId_RetriesWithResolvedLocalUser()
	{
		const string localUserId = "profile-local-user";
		const string externalUserId = "profile-external-user";
		const string email = "profile@example.com";
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			await SeedUserAsync(setupContext, localUserId, userName: "profile-user", email);
			await SeedPreferencesAsync(setupContext, localUserId, 1, 2, 5, 10);
		}

		await using var context = CreateRetryOnceContext(databaseName, databaseRoot);
		var sut = new UpdateGamePreferencesCommandHandler(
			context,
			CreateCurrentUserService(
				isAuthenticated: true,
				userId: externalUserId,
				userEmail: email));

		var result = await sut.Handle(new UpdateGamePreferencesCommand(15, 30, 6, 30), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.DefaultSmallBlind.Should().Be(15);
		result.AsT0.DefaultBigBlind.Should().Be(30);
		result.AsT0.DefaultAnte.Should().Be(6);
		result.AsT0.DefaultMinimumBet.Should().Be(30);

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedPreferences = await verificationContext.UserGamePreferences
			.OrderBy(preferences => preferences.UserId)
			.ToListAsync();

		persistedPreferences.Should().ContainSingle();
		persistedPreferences[0].UserId.Should().Be(localUserId);
		persistedPreferences[0].DefaultSmallBlind.Should().Be(15);
		persistedPreferences[0].DefaultBigBlind.Should().Be(30);
		persistedPreferences[0].DefaultAnte.Should().Be(6);
		persistedPreferences[0].DefaultMinimumBet.Should().Be(30);
		persistedPreferences.Should().NotContain(preferences => preferences.UserId == externalUserId);
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

	private static RetryOnceCardsDbContext CreateRetryOnceContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot)
			.Options;

		return new RetryOnceCardsDbContext(options);
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

	private sealed class RetryOnceCardsDbContext(DbContextOptions<CardsDbContext> options) : CardsDbContext(options)
	{
		private bool shouldFail = true;

		public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
		{
			if (shouldFail && ChangeTracker.Entries<UserGamePreferences>().Any(entry => entry.State != EntityState.Unchanged))
			{
				shouldFail = false;
				throw new DbUpdateException("Simulated stale user mapping conflict.", new Exception("Simulated save failure."));
			}

			return base.SaveChangesAsync(cancellationToken);
		}
	}
}