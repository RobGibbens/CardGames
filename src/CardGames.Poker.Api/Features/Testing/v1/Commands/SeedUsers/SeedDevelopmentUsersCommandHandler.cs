using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Profile.v1.Cashier;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneOf;

namespace CardGames.Poker.Api.Features.Testing.v1.Commands.SeedUsers;

public sealed class SeedDevelopmentUsersCommandHandler(
    IOptions<DevelopmentUserSeedOptions> options,
    UserManager<ApplicationUser> userManager,
    CardsDbContext context,
    ILogger<SeedDevelopmentUsersCommandHandler> logger)
    : IRequestHandler<SeedDevelopmentUsersCommand, OneOf<SeedDevelopmentUsersResponse, SeedDevelopmentUsersError>>
{
    public async Task<OneOf<SeedDevelopmentUsersResponse, SeedDevelopmentUsersError>> Handle(
        SeedDevelopmentUsersCommand request,
        CancellationToken cancellationToken)
    {
        var configuredUsers = options.Value.Users;

        if (configuredUsers.Count == 0)
        {
            return new SeedDevelopmentUsersError(
                SeedDevelopmentUsersErrorCode.NoUsersConfigured,
                $"No users are configured under '{DevelopmentUserSeedOptions.SectionName}:Users'.");
        }

        var results = new List<SeedDevelopmentUserResult>(configuredUsers.Count);
        var createdCount = 0;
        var skippedCount = 0;
        var failedCount = 0;
        const int seedChipAmount = CashierAccountInitializer.StartingChipAmount;

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

            var now = DateTimeOffset.UtcNow;

            var player = await context.Players
                .FirstOrDefaultAsync(p => p.Email == email, cancellationToken);

            if (player is null)
            {
                player = new Player
                {
                    Id = Guid.CreateVersion7(),
                    Name = email,
                    Email = email,
                    ExternalId = user.Id,
                    AvatarUrl = user.AvatarUrl,
                    CreatedAt = now,
                    UpdatedAt = now,
                    LastActiveAt = now,
                    IsActive = true
                };

                context.Players.Add(player);
            }

            await CashierAccountInitializer.EnsureAccountInitializedAsync(
                context,
                player.Id,
                seedChipAmount,
                "DevelopmentSeed",
                "Initial development seed chips",
                null,
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            createdCount++;
            results.Add(new SeedDevelopmentUserResult
            {
                Email = email,
                Status = "created",
                Message = $"User created, email confirmed, and {seedChipAmount:N0} chips added."
            });

            logger.LogInformation(
                "Created development seed user {Email} with {ChipAmount} chips (PlayerId={PlayerId})",
                email,
                seedChipAmount,
                player.Id);
        }

        return new SeedDevelopmentUsersResponse
        {
            ConfiguredCount = configuredUsers.Count,
            CreatedCount = createdCount,
            SkippedCount = skippedCount,
            FailedCount = failedCount,
            Users = results
        };
    }
}
