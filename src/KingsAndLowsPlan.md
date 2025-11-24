# Kings and Lows (Kings and Little Ones) - Implementation Plan

## Game Overview

**Kings and Lows** (also known as "Kings and Little Ones") is a Seven Card Stud variant with wild cards:
- **Wild Cards**: Kings + the lowest-value card in each player's hand
- **Optional Rule**: A King may be required in hand for the low card to be wild
- **Deal Structure**: Identical to Seven Card Stud (2 down, 1 up, 3 up, 1 up/down)
- **Special Hand**: Five of a Kind becomes possible due to wild cards

## Domain Analysis

### Key Differences from Standard Seven Card Stud
1. **Wild Cards**: Dynamic wild cards based on hand composition
2. **Hand Evaluation**: Must support Five of a Kind (beats Straight Flush)
3. **Low Card Determination**: Must identify the lowest-value card per player
4. **King Requirement**: Optional rule variation
5. **Final Card Choice**: "Down and Dirty" - player chooses face-up or face-down for 7th card

### Wild Card Rules
- **Kings**: Always wild
- **Lowest Card**: The card(s) with the minimum value in a player's hand
  - If player has multiple cards of lowest value, ALL are wild
  - Example: Hand has 2h, 2d, 5c, 8s, Kh ? Both 2h and 2d are wild (plus the King)
- **Optional Constraint**: If "King Required" variant, low cards are only wild if hand contains a King

## Implementation Strategy

### Phase 1: Core Domain Extensions

#### 1.1 Extend `HandType` Enum
**File**: `CardGames.Poker\Hands\HandTypes\HandType.cs`

Add `FiveOfAKind` to the enum (highest ranking hand):
```csharp
public enum HandType
{
    Incomplete,
    HighCard,
    OnePair,
    TwoPair,
    Trips,
    Straight,
    Flush,
    FullHouse,
    Quads,
    StraightFlush,
    FiveOfAKind  // New - highest hand
}
```

#### 1.2 Update `HandTypeDetermination` 
**File**: `CardGames.Poker\Hands\HandTypes\HandTypeDetermination.cs`

Add logic to detect Five of a Kind:
```csharp
public static HandType DetermineHandType(IReadOnlyCollection<Card> cards)
{
    if (cards.Count < 5)
    {
        return HandType.Incomplete;
    }

    var numberOfDistinctValues = cards.DistinctValues().Count;
    
    // Check for Five of a Kind first (requires exactly 1 distinct value)
    if (numberOfDistinctValues == 1)
    {
        return HandType.FiveOfAKind;
    }

    return numberOfDistinctValues == 5
        ? HandTypeOfDistinctValueHand(cards)
        : HandTypeOfDuplicateValueHand(cards, numberOfDistinctValues);
}
```

#### 1.3 Update `HandTypeStrengthRanking`
**File**: `CardGames.Poker\Hands\Strength\HandTypeStrength.cs`

Add Five of a Kind to ranking (above Straight Flush):
```csharp
public static class HandTypeStrengthRanking
{
    public static readonly HandTypeStrengthRanking Classic = new()
    {
        [HandType.FiveOfAKind] = 10,      // New - highest
        [HandType.StraightFlush] = 9,
        [HandType.Quads] = 8,
        // ... rest unchanged
    };
}
```

#### 1.4 Update Serialization Extensions
**File**: `CardGames.Poker\Hands\Extensions\SerializationExtensions.cs`

Add display string for Five of a Kind:
```csharp
HandType.FiveOfAKind => "Five of a Kind",
```

### Phase 2: Wild Card Support

#### 2.1 Create Wild Card Domain Model
**New File**: `CardGames.Poker\Hands\WildCards\WildCardRules.cs`

```csharp
namespace CardGames.Poker.Hands.WildCards;

public class WildCardRules
{
    public bool KingRequired { get; }
    
    public WildCardRules(bool kingRequired = false)
    {
        KingRequired = kingRequired;
    }

    public IReadOnlyCollection<Card> DetermineWildCards(IReadOnlyCollection<Card> hand)
    {
        var wildCards = new List<Card>();
        
        // Kings are always wild
        wildCards.AddRange(hand.Where(c => c.Symbol == Symbol.King));
        
        // Determine if low cards can be wild
        var hasKing = wildCards.Any();
        if (!KingRequired || hasKing)
        {
            var minValue = hand.Min(c => c.Value);
            wildCards.AddRange(hand.Where(c => c.Value == minValue));
        }
        
        return wildCards.Distinct().ToList();
    }
}
```

#### 2.2 Create Wild Card Hand Evaluator
**New File**: `CardGames.Poker\Hands\WildCards\WildCardHandEvaluator.cs`

This is the complex part - evaluating all possible combinations when wild cards can represent any card:

```csharp
namespace CardGames.Poker.Hands.WildCards;

public static class WildCardHandEvaluator
{
    public static (HandType Type, long Strength) EvaluateBestHand(
        IReadOnlyCollection<Card> allCards,
        IReadOnlyCollection<Card> wildCards,
        HandTypeStrengthRanking ranking)
    {
        // Get all 5-card combinations from 7 cards
        var fiveCardHands = allCards.SubsetsOfSize(5);
        
        var bestType = HandType.Incomplete;
        var bestStrength = 0L;
        
        foreach (var hand in fiveCardHands)
        {
            var wildInHand = hand.Intersect(wildCards).ToList();
            
            if (wildInHand.Any())
            {
                // Evaluate with wild card substitutions
                var (type, strength) = EvaluateWithWildCards(hand, wildInHand, ranking);
                if (IsStronger(type, strength, bestType, bestStrength, ranking))
                {
                    bestType = type;
                    bestStrength = strength;
                }
            }
            else
            {
                // Standard evaluation
                var type = HandTypeDetermination.DetermineHandType(hand);
                var strength = HandStrength.Calculate(hand, type, ranking);
                if (IsStronger(type, strength, bestType, bestStrength, ranking))
                {
                    bestType = type;
                    bestStrength = strength;
                }
            }
        }
        
        return (bestType, bestStrength);
    }
    
    private static (HandType, long) EvaluateWithWildCards(
        IReadOnlyCollection<Card> hand,
        IReadOnlyCollection<Card> wildCards,
        HandTypeStrengthRanking ranking)
    {
        // This requires generating all possible substitutions
        // For each wild card, try substituting it with each possible value
        // Keep the best result
        
        // Start with Five of a Kind check - easiest case
        var naturalCards = hand.Except(wildCards).ToList();
        
        // Can we make Five of a Kind?
        if (naturalCards.Count > 0)
        {
            var mostCommonValue = naturalCards
                .GroupBy(c => c.Value)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;
                
            var countOfValue = naturalCards.Count(c => c.Value == mostCommonValue);
            if (countOfValue + wildCards.Count >= 5)
            {
                // We can make Five of a Kind!
                return (HandType.FiveOfAKind, 
                        HandStrength.CalculateFiveOfAKind(mostCommonValue));
            }
        }
        
        // Otherwise, try other combinations...
        // (Implementation would need to be thorough)
    }
}
```

**Note**: The wild card evaluation is computationally intensive. A production implementation should:
1. Use memoization/caching for common patterns
2. Implement efficient pruning (don't evaluate worse combinations)
3. Consider using lookup tables for common wild card scenarios
4. May benefit from benchmarking and optimization

### Phase 3: Kings and Lows Hand Implementation

#### 3.1 Create KingsAndLowsHand
**New File**: `CardGames.Poker\Hands\StudHands\KingsAndLowsHand.cs`

```csharp
namespace CardGames.Poker.Hands.StudHands;

public class KingsAndLowsHand : StudHand
{
    private readonly WildCardRules _wildCardRules;
    private IReadOnlyCollection<Card> _wildCards;
    
    public IReadOnlyCollection<Card> WildCards => _wildCards ??= DetermineWildCards();

    public KingsAndLowsHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> openCards,
        Card downCard,
        WildCardRules wildCardRules) 
        : base(holeCards, openCards, new[] { downCard })
    {
        if (holeCards.Count != 2)
        {
            throw new ArgumentException("Kings and Lows needs exactly two hole cards");
        }
        if (openCards.Count > 4)
        {
            throw new ArgumentException("Kings and Lows has at most four open cards");
        }
        
        _wildCardRules = wildCardRules;
    }
    
    private IReadOnlyCollection<Card> DetermineWildCards()
        => _wildCardRules.DetermineWildCards(Cards);
    
    protected override long CalculateStrength()
    {
        if (!WildCards.Any())
        {
            return base.CalculateStrength(); // Standard evaluation
        }
        
        // Use wild card evaluator
        var (type, strength) = WildCardHandEvaluator.EvaluateBestHand(
            Cards, WildCards, Ranking);
        _type = type;
        return strength;
    }
    
    protected override HandType DetermineType()
    {
        if (!WildCards.Any())
        {
            return base.DetermineType();
        }
        
        // Will be set during CalculateStrength
        return HandType.Incomplete;
    }
}
```

### Phase 4: Simulation Support

#### 4.1 Create KingsAndLowsPlayer
**New File**: `CardGames.Playground\Simulations\Stud\KingsAndLowsPlayer.cs`

```csharp
namespace CardGames.Playground.Simulations.Stud;

public class KingsAndLowsPlayer : StudPlayer
{
    public bool LastCardFaceUp { get; set; } = false;
    
    public KingsAndLowsPlayer(string name) : base(name)
    {
    }
    
    public KingsAndLowsPlayer WithLastCardFaceUp(bool faceUp = true)
    {
        LastCardFaceUp = faceUp;
        return this;
    }
}
```

#### 4.2 Create KingsAndLowsSimulation
**New File**: `CardGames.Playground\Simulations\Stud\KingsAndLowsSimulation.cs`

```csharp
namespace CardGames.Playground.Simulations.Stud;

public class KingsAndLowsSimulation
{
    private FrenchDeckDealer _dealer;
    private readonly List<KingsAndLowsPlayer> _players = new();
    private IReadOnlyCollection<Card> _deadCards = new List<Card>();
    private WildCardRules _wildCardRules = new WildCardRules(kingRequired: false);
    
    public KingsAndLowsSimulation WithPlayer(KingsAndLowsPlayer player)
    {
        _players.Add(player);
        return this;
    }
    
    public KingsAndLowsSimulation WithDeadCards(IReadOnlyCollection<Card> cards)
    {
        _deadCards = cards;
        return this;
    }
    
    public KingsAndLowsSimulation WithKingRequired(bool required = true)
    {
        _wildCardRules = new WildCardRules(kingRequired: required);
        return this;
    }
    
    public StudSimulationResult Simulate(int nrOfHands)
    {
        _dealer = FrenchDeckDealer.WithFullDeck();
        return Play(nrOfHands);
    }
    
    private StudSimulationResult Play(int nrOfHands)
    {
        var results = Enumerable
            .Range(1, nrOfHands)
            .Select(_ => PlayHand());
            
        return new StudSimulationResult(nrOfHands, results.ToList());
    }
    
    private IDictionary<string, KingsAndLowsHand> PlayHand()
    {
        _dealer.Shuffle();
        RemovePlayerCardsFromDeck();
        RemoveDeadCardsFromDeck();
        DealMissingHoleCards();
        DealMissingBoardCards();
        
        return _players.ToDictionary(
            player => player.Name,
            player => new KingsAndLowsHand(
                player.HoleCards.Take(2).ToList(),
                player.BoardCards.ToList(),
                player.HoleCards.Last(),
                _wildCardRules));
    }
    
    // Similar helper methods as SevenCardStudSimulation
    // RemoveDeadCardsFromDeck(), RemovePlayerCardsFromDeck(), etc.
}
```

### Phase 5: Testing

#### 5.1 Unit Tests for Wild Card Detection
**New File**: `Tests\CardGames.Poker.Tests\Hands\WildCards\WildCardRulesTests.cs`

Test cases:
- Kings are always wild
- Lowest cards are wild (single)
- Multiple lowest cards all wild (e.g., two 2s)
- King required: low cards NOT wild without King
- King required: low cards ARE wild with King
- Edge case: All cards same value (all wild)

#### 5.2 Unit Tests for Hand Evaluation
**New File**: `Tests\CardGames.Poker.Tests\Hands\WildCards\WildCardHandEvaluatorTests.cs`

Test cases:
- Five of a Kind detection with wild cards
- Natural Five of a Kind (if all same, all wild)
- Straight Flush with wild cards
- Quads vs Five of a Kind ranking
- Multiple wild cards creating best possible hand

#### 5.3 Integration Tests for KingsAndLowsHand
**New File**: `Tests\CardGames.Poker.Tests\Hands\StudHands\KingsAndLowsHandTests.cs`

Test cases:
- Hand with Kings beats same hand without
- Low card as wild improves hand
- King required rule enforcement
- Comparison between hands with different wild cards
- Five of a Kind beats Straight Flush

#### 5.4 Simulation Tests
**New File**: `Tests\CardGames.Poker.Tests\Simulations\KingsAndLowsSimulationTests.cs`

Test cases:
- Basic simulation runs without errors
- Players with known cards produce expected wild cards
- King required setting affects results
- "Down and Dirty" option tracking

### Phase 6: Performance Optimization

Wild card evaluation is computationally expensive. After initial implementation:

1. **Profile**: Use BenchmarkDotNet to identify bottlenecks
2. **Optimize Common Cases**: 
   - No wild cards ? fast path (existing evaluation)
   - One wild card ? limited substitutions
   - Multiple wild cards ? needs efficient pruning
3. **Consider Lookup Tables**: Pre-compute common wild card scenarios
4. **Memoization**: Cache wild card evaluations within a hand

**New File**: `CardGames.Poker.Benchmarks\WildCardBenchmarks.cs`

```csharp
[MemoryDiagnoser]
public class WildCardBenchmarks
{
    [Benchmark]
    public void EvaluateHand_NoWildCards() { }
    
    [Benchmark]
    public void EvaluateHand_OneWildCard() { }
    
    [Benchmark]
    public void EvaluateHand_TwoWildCards() { }
    
    [Benchmark]
    public void EvaluateHand_ThreeWildCards() { }
}
```

## Implementation Order

1. **Phase 1**: Core extensions (HandType, HandTypeDetermination, etc.)
   - Low risk, foundational
   - Easy to test
   
2. **Phase 2**: Wild card domain model
   - Medium complexity
   - Core game logic
   
3. **Phase 3**: Hand implementation
   - Depends on Phase 1 & 2
   - Integrates everything
   
4. **Phase 4**: Simulation support
   - Depends on Phase 3
   - Makes it usable
   
5. **Phase 5**: Comprehensive testing
   - Parallel to implementation
   - Critical for correctness
   
6. **Phase 6**: Performance tuning
   - After correctness proven
   - Based on measurements

## Example Usage

```csharp
// Create a Kings and Lows simulation
var result = new KingsAndLowsSimulation()
    .WithPlayer(
        new KingsAndLowsPlayer("Alice")
            .WithHoleCards("Kh 2d".ToCards())
            .WithBoardCards("3c".ToCards()))
    .WithPlayer(
        new KingsAndLowsPlayer("Bob")
            .WithHoleCards("As".ToCards())
            .WithBoardCards("Ac".ToCards()))
    .WithKingRequired(false)  // Low cards always wild
    .Simulate(10000);

// Alice has King (wild) + 2 (wild as lowest)
// Bob needs to catch up
```

## Design Decisions

### Wild Card Representation
- **Decision**: Calculate wild cards dynamically, not store as separate property
- **Rationale**: Wild cards depend on entire hand composition; can't be determined partially

### Five of a Kind Implementation
- **Decision**: Add to HandType enum rather than special case
- **Rationale**: Keeps hand evaluation consistent; extends cleanly

### King Required Variant
- **Decision**: Pass as constructor parameter to WildCardRules
- **Rationale**: Immutable rule for entire simulation; clear configuration

### "Down and Dirty" Choice
- **Decision**: Track in player model, doesn't affect simulation logic
- **Rationale**: In simulation, final card is random either way; only affects display/strategy

## Potential Challenges

1. **Wild Card Complexity**: Evaluating all substitutions is O(n^w) where w = wild cards
   - Mitigation: Efficient pruning, caching, early termination
   
2. **Five of a Kind Rarity**: May need many hands to see in practice
   - Mitigation: Unit tests with forced scenarios
   
3. **Multiple Low Cards**: When player has pair of lowest value, both are wild
   - Mitigation: Clear documentation, explicit test cases
   
4. **Hand Strength Calculation**: Wild cards break some assumptions in existing code
   - Mitigation: Override calculation methods in KingsAndLowsHand

## Testing Strategy

- **Unit Tests**: Individual wild card detection, hand type determination
- **Integration Tests**: Full hand evaluation with various wild card scenarios
- **Simulation Tests**: End-to-end with known starting hands
- **Property Tests**: Invariants (Five of a Kind > Straight Flush, etc.)
- **Performance Tests**: Benchmarks for wild card evaluation

## Documentation Needs

1. Add Kings and Lows section to README.md
2. Document wild card rules clearly
3. Add usage examples
4. Document performance characteristics
5. Explain "King Required" variant

## Future Enhancements

1. **Visualization**: Show wild cards differently in output
2. **Strategy Analysis**: What percentage of hands improve with wild cards?
3. **Other Variants**: "Baseball" (3s and 9s wild), "Deuces Wild", etc.
4. **Hand History**: Track which cards were wild in each hand
5. **Probability Calculator**: Odds of making Five of a Kind with X wild cards

## Alignment with Project Philosophy

- ? **Domain-Driven**: Models real poker variant accurately
- ? **Clean Architecture**: Extends existing abstractions cleanly
- ? **Performance-Aware**: Includes benchmarking strategy
- ? **Testable**: Comprehensive test plan
- ? **Maintainable**: Follows existing patterns and conventions
- ? **Fluent API**: Simulation builders consistent with existing code
