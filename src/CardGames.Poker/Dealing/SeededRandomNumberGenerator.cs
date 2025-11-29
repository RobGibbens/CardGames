using System;
using CardGames.Core.Random;

namespace CardGames.Poker.Dealing;

/// <summary>
/// A random number generator that uses a seed for deterministic, reproducible dealing.
/// Used for replay mode and testing.
/// </summary>
public class SeededRandomNumberGenerator : IRandomNumberGenerator
{
    private readonly Random _random;
    
    /// <summary>
    /// Gets the seed used to initialize this generator.
    /// </summary>
    public int Seed { get; }

    /// <summary>
    /// Creates a new seeded random number generator with the specified seed.
    /// </summary>
    /// <param name="seed">The seed for deterministic random generation.</param>
    public SeededRandomNumberGenerator(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    /// <summary>
    /// Creates a new seeded random number generator with a random seed.
    /// Uses multiple entropy sources for better unpredictability.
    /// </summary>
    public SeededRandomNumberGenerator()
        : this(GenerateRandomSeed())
    {
    }

    /// <inheritdoc/>
    public int Next(int upperBound)
        => _random.Next(upperBound);

    /// <summary>
    /// Creates a new generator with a random seed and returns both the generator and the seed.
    /// </summary>
    public static (SeededRandomNumberGenerator generator, int seed) CreateWithRandomSeed()
    {
        var seed = GenerateRandomSeed();
        return (new SeededRandomNumberGenerator(seed), seed);
    }

    /// <summary>
    /// Generates a random seed using multiple entropy sources.
    /// </summary>
    private static int GenerateRandomSeed()
    {
        // Combine multiple entropy sources for better unpredictability
        return unchecked(
            Environment.TickCount64.GetHashCode() ^ 
            Guid.NewGuid().GetHashCode() ^ 
            DateTime.UtcNow.Ticks.GetHashCode());
    }
}
