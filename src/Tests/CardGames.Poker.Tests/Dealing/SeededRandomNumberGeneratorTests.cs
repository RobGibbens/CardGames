using CardGames.Poker.Dealing;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Dealing;

public class SeededRandomNumberGeneratorTests
{
    [Fact]
    public void Constructor_WithSeed_StoresSeed()
    {
        var rng = new SeededRandomNumberGenerator(42);
        
        rng.Seed.Should().Be(42);
    }

    [Fact]
    public void Next_ReturnsValueInRange()
    {
        var rng = new SeededRandomNumberGenerator(42);
        
        for (int i = 0; i < 100; i++)
        {
            var value = rng.Next(10);
            value.Should().BeInRange(0, 9);
        }
    }

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var rng1 = new SeededRandomNumberGenerator(42);
        var rng2 = new SeededRandomNumberGenerator(42);
        
        for (int i = 0; i < 50; i++)
        {
            rng1.Next(100).Should().Be(rng2.Next(100));
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        var rng1 = new SeededRandomNumberGenerator(1);
        var rng2 = new SeededRandomNumberGenerator(2);
        
        var sequence1 = new int[10];
        var sequence2 = new int[10];
        
        for (int i = 0; i < 10; i++)
        {
            sequence1[i] = rng1.Next(1000);
            sequence2[i] = rng2.Next(1000);
        }
        
        sequence1.Should().NotBeEquivalentTo(sequence2);
    }

    [Fact]
    public void CreateWithRandomSeed_GeneratesGenerator()
    {
        var (generator, seed) = SeededRandomNumberGenerator.CreateWithRandomSeed();
        
        generator.Should().NotBeNull();
        generator.Seed.Should().Be(seed);
    }

    [Fact]
    public void CreateWithRandomSeed_GeneratorIsUsable()
    {
        var (generator, _) = SeededRandomNumberGenerator.CreateWithRandomSeed();
        
        // Should not throw
        var value = generator.Next(100);
        value.Should().BeInRange(0, 99);
    }

    [Fact]
    public void DefaultConstructor_GeneratesSeed()
    {
        var rng = new SeededRandomNumberGenerator();
        
        // Seed should be set to some value
        rng.Seed.Should().NotBe(0);
    }

    [Fact]
    public void Deterministic_Replay_Works()
    {
        // Simulate capturing a seed for replay
        var (originalRng, capturedSeed) = SeededRandomNumberGenerator.CreateWithRandomSeed();
        
        var originalSequence = new int[20];
        for (int i = 0; i < 20; i++)
        {
            originalSequence[i] = originalRng.Next(52);
        }
        
        // Now replay with the captured seed
        var replayRng = new SeededRandomNumberGenerator(capturedSeed);
        var replaySequence = new int[20];
        for (int i = 0; i < 20; i++)
        {
            replaySequence[i] = replayRng.Next(52);
        }
        
        replaySequence.Should().BeEquivalentTo(originalSequence);
    }
}
