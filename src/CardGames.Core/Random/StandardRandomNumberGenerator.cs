namespace CardGames.Core.Random;

public class StandardRandomNumberGenerator : IRandomNumberGenerator
{
    // Use Random.Shared for thread-safe, properly seeded random number generation
    // This prevents multiple instances created in quick succession from having identical sequences
    private readonly System.Random _random = System.Random.Shared;

    public int Next(int upperBound)
        => _random.Next(upperBound);
}
