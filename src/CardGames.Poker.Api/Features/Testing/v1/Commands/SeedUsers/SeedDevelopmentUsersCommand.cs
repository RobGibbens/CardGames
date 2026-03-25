using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Testing.v1.Commands.SeedUsers;

/// <summary>
/// Seeds the configured development test users.
/// </summary>
public sealed record SeedDevelopmentUsersCommand : IRequest<OneOf<SeedDevelopmentUsersResponse, SeedDevelopmentUsersError>>;

public enum SeedDevelopmentUsersErrorCode
{
    NoUsersConfigured
}

public sealed record SeedDevelopmentUsersError(SeedDevelopmentUsersErrorCode Code, string Message);
