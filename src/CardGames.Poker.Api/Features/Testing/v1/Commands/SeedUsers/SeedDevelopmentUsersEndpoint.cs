using CardGames.Poker.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Features.Testing.v1.Commands.SeedUsers;

public static class SeedDevelopmentUsersEndpoint
{
	public static RouteGroupBuilder MapSeedDevelopmentUsers(this RouteGroupBuilder group)
	{
		group.MapPost("users/seed",
				async (
					IOptions<DevelopmentUserSeedOptions> options,
					UserManager<ApplicationUser> userManager,
					ILoggerFactory loggerFactory,
					CancellationToken cancellationToken) =>
				{
					var logger = loggerFactory.CreateLogger("DevelopmentUserSeeder");
					var configuredUsers = options.Value.Users;

					if (configuredUsers.Count == 0)
					{
						return Results.Problem(
							detail: $"No users are configured under '{DevelopmentUserSeedOptions.SectionName}:Users'.",
							statusCode: StatusCodes.Status400BadRequest);
					}

					var results = new List<SeedDevelopmentUserResult>(configuredUsers.Count);
					var createdCount = 0;
					var skippedCount = 0;
					var failedCount = 0;

					foreach (var configuredUser in configuredUsers)
					{
						cancellationToken.ThrowIfCancellationRequested();

						var email = configuredUser.Email.Trim();
						if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(configuredUser.Password))
						{
							failedCount++;
							results.Add(new SeedDevelopmentUserResult
							{
								Email = email,
								Status = "failed",
								Message = "Email and password are required."
							});

							continue;
						}

						var existingUser = await userManager.FindByEmailAsync(email);
						if (existingUser is not null)
						{
							skippedCount++;
							results.Add(new SeedDevelopmentUserResult
							{
								Email = email,
								Status = "skipped",
								Message = "User already exists."
							});

							continue;
						}

						var user = new ApplicationUser
						{
							UserName = email,
							Email = email,
							FirstName = configuredUser.FirstName.Trim(),
							LastName = configuredUser.LastName.Trim(),
							PhoneNumber = string.IsNullOrWhiteSpace(configuredUser.PhoneNumber)
								? null
								: configuredUser.PhoneNumber.Trim(),
							AvatarUrl = string.IsNullOrWhiteSpace(configuredUser.AvatarUrl)
								? null
								: configuredUser.AvatarUrl.Trim(),
							EmailConfirmed = true
						};

						var createResult = await userManager.CreateAsync(user, configuredUser.Password);
						if (!createResult.Succeeded)
						{
							failedCount++;
							var errorMessage = string.Join(" ", createResult.Errors.Select(error => error.Description));
							logger.LogWarning(
								"Failed to create development seed user {Email}: {Message}",
								email,
								errorMessage);

							results.Add(new SeedDevelopmentUserResult
							{
								Email = email,
								Status = "failed",
								Message = errorMessage
							});

							continue;
						}

						createdCount++;
						results.Add(new SeedDevelopmentUserResult
						{
							Email = email,
							Status = "created",
							Message = "User created and email confirmed."
						});

						logger.LogInformation("Created development seed user {Email}", email);
					}

					return Results.Ok(new SeedDevelopmentUsersResponse
					{
						ConfiguredCount = configuredUsers.Count,
						CreatedCount = createdCount,
						SkippedCount = skippedCount,
						FailedCount = failedCount,
						Users = results
					});
				})
			.WithName("SeedDevelopmentUsers")
			.WithSummary("Seed development login users")
			.WithDescription("Creates the configured development test users, marks them as confirmed, and skips any account that already exists.")
			.Produces<SeedDevelopmentUsersResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.AllowAnonymous();

		return group;
	}
}