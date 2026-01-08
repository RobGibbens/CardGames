using System;

namespace CardGames.Core.Random;

public class StandardRandomNumberGenerator : IRandomNumberGenerator
{
    // Use a GUID-based seed to ensure each instance has a unique sequence
    // This prevents multiple instances from producing identical sequences
    private readonly System.Random _random = new System.Random(Guid.NewGuid().GetHashCode());

    public int Next(int upperBound)
        => _random.Next(upperBound);
}
