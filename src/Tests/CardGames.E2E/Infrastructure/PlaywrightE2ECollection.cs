[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     One shared Aspire app and browser for the suite. Tests in this collection run one at a time
///     because they share a single database.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PlaywrightE2ECollection : ICollectionFixture<PlaywrightAppFixture>
{
	public const string Name = "PlaywrightE2E";
}
