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
    /// </summary>
    public SeededRandomNumberGenerator()
        : this(Environment.TickCount)
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
        var seed = Environment.TickCount ^ Guid.NewGuid().GetHashCode();
        return (new SeededRandomNumberGenerator(seed), seed);
    }
}
