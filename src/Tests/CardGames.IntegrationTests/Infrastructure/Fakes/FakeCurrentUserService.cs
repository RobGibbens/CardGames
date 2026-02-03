using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Infrastructure.Fakes;

public class FakeCurrentUserService : ICurrentUserService
{
    public string? UserId { get; set; } = "test-user-id";

    public string? UserName { get; set; } = "Test User";

    public string? UserEmail { get; set; } = "test@example.com";

    public bool IsAuthenticated { get; set; } = true;
}
