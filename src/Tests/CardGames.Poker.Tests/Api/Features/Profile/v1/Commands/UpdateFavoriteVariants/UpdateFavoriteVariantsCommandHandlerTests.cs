#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Profile.v1.Commands.UpdateFavoriteVariants;
using CardGames.Poker.Api.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Profile.v1.Commands.UpdateFavoriteVariants;

public class UpdateFavoriteVariantsCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenUserIsUnauthenticated_ReturnsUnauthorized()
	{
		await using var context = CreateContext();
		var sut = new UpdateFavoriteVariantsCommandHandler(
			context,
			CreateCurrentUserService(isAuthenticated: false, userId: null));

		var result = await sut.Handle(new UpdateFavoriteVariantsCommand(["holdem"]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(UpdateFavoriteVariantsErrorCode.Unauthorized);
	}

	[Fact]
	public async Task Handle_WhenFavoriteVariantCodesContainIncorrectData_NormalizesAndPersistsDistinctValues()
	{
		const string userId = "favorite-user";
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			await SeedUserAsync(setupContext, userId, userName: "favorite-user", email: "favorite@example.com");
		}

		await using var context = CreateContext(databaseName, databaseRoot);
		var sut = new UpdateFavoriteVariantsCommandHandler(
			context,
			CreateCurrentUserService(isAuthenticated: true, userId: userId));

		var result = await sut.Handle(
			new UpdateFavoriteVariantsCommand([" holdem ", "", "DEALERSCHOICE", "HoldEm", "  "]),
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.FavoriteVariantCodes.Should().Equal(["DEALERSCHOICE", "HOLDEM"]);

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedPreferences = await verificationContext.UserGamePreferences.SingleAsync(preferences => preferences.UserId == userId);
		persistedPreferences.FavoriteVariantCodesJson.Should().Be("[\"DEALERSCHOICE\",\"HOLDEM\"]");
	}

	[Fact]
	public async Task Handle_WhenInitialSaveFailsForStaleUserId_RetriesWithResolvedLocalUser()
	{
		const string localUserId = "favorite-local-user";
		const string externalUserId = "favorite-external-user";
		const string userName = "favorite-profile";
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			await SeedUserAsync(setupContext, localUserId, userName, email: "favorite-profile@example.com");
			await SeedPreferencesAsync(setupContext, localUserId, "[\"SEVENCARDSTUD\"]");
		}

		await using var context = CreateRetryOnceContext(databaseName, databaseRoot);
		var sut = new UpdateFavoriteVariantsCommandHandler(
			context,
			CreateCurrentUserService(
				isAuthenticated: true,
				userId: externalUserId,
				userName: userName));

		var result = await sut.Handle(new UpdateFavoriteVariantsCommand(["holdem", "dealerschoice"]), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.FavoriteVariantCodes.Should().Equal(["DEALERSCHOICE", "HOLDEM"]);

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedPreferences = await verificationContext.UserGamePreferences
			.OrderBy(preferences => preferences.UserId)
			.ToListAsync();

		persistedPreferences.Should().ContainSingle();
		persistedPreferences[0].UserId.Should().Be(localUserId);
		persistedPreferences[0].FavoriteVariantCodesJson.Should().Be("[\"DEALERSCHOICE\",\"HOLDEM\"]");
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