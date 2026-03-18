using System.Net;
using System.Net.Http.Json;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Testing;
using CardGames.Poker.Api.Features.Testing.v1.Commands.SeedUsers;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CardGames.IntegrationTests.Api;

public class TestingApiSeedUsersTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	[Fact]
	public async Task SeedUsers_Endpoint_CreatesConfirmedAccounts_AndSkipsExistingUsersOnReplay()
	{
		var firstResponse = await Client.PostAsync("/api/v1/testing/users/seed", content: null);

		firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var firstPayload = await firstResponse.Content.ReadFromJsonAsync<SeedDevelopmentUsersResponse>();
		firstPayload.Should().NotBeNull();

		using var verificationScope = Factory.Services.CreateScope();
		var configuredUsers = verificationScope.ServiceProvider
			.GetRequiredService<IOptions<DevelopmentUserSeedOptions>>()
			.Value
			.Users;
		var userManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

		firstPayload!.ConfiguredCount.Should().Be(configuredUsers.Count);
		firstPayload.CreatedCount.Should().Be(configuredUsers.Count);
		firstPayload.SkippedCount.Should().Be(0);
		firstPayload.FailedCount.Should().Be(0);

		foreach (var configuredUser in configuredUsers)
		{
			var createdUser = await userManager.FindByEmailAsync(configuredUser.Email);
			createdUser.Should().NotBeNull();
			createdUser!.EmailConfirmed.Should().BeTrue();
			createdUser.FirstName.Should().Be(configuredUser.FirstName);
			createdUser.LastName.Should().Be(configuredUser.LastName);
			createdUser.PhoneNumber.Should().Be(configuredUser.PhoneNumber);
			(await userManager.CheckPasswordAsync(createdUser, configuredUser.Password)).Should().BeTrue();
		}

		var secondResponse = await Client.PostAsync("/api/v1/testing/users/seed", content: null);

		secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var secondPayload = await secondResponse.Content.ReadFromJsonAsync<SeedDevelopmentUsersResponse>();
		secondPayload.Should().NotBeNull();
		secondPayload!.ConfiguredCount.Should().Be(configuredUsers.Count);
		secondPayload.CreatedCount.Should().Be(0);
		secondPayload.SkippedCount.Should().Be(configuredUsers.Count);
		secondPayload.FailedCount.Should().Be(0);
		secondPayload.Users.Should().OnlyContain(user => user.Status == "skipped");
	}
}