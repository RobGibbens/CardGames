# ADR-003: Generic Card Type Architecture

## Status

Accepted

## Context

The CardGames library needs to support multiple types of card games with different card types:

1. **French-suited playing cards** (Hearts, Diamonds, Clubs, Spades)
2. **Short deck** (36 cards, Six-Ace only)
3. **Custom card types** (potential future: Tarot, Uno, etc.)
4. **Non-card randomness** (dice can be modeled as cards)

We need a design that:
- Maximizes code reuse across game types
- Maintains type safety
- Performs well (cards are created and passed frequently)
- Allows for specialized behavior per card type

## Decision

We will use a **generic type parameter `TCard`** constrained to **class** types for all core abstractions.

### Core Interfaces and Classes

```csharp
// Generic deck interface
public interface IDeck<TCard> where TCard : class
{
    int NumberOfCardsLeft();
    TCard GetFromRemaining(int index);
    TCard GetSpecific(TCard specificCard);
    void Reset();
}

// Generic dealer
public class Dealer<TCard> where TCard : class
{
    protected IDeck<TCard> Deck { get; }
    protected IRandomNumberGenerator NumberGenerator { get; }
    
    public TCard DealCard() { ... }
    public IReadOnlyCollection<TCard> DealCards(int amount) { ... }
    public void Shuffle() { ... }
}
```

### Constraint: `where TCard : class`

We chose `class` constraint over `struct` because:

1. **Pass-by-reference is more efficient** for cards that are frequently passed between methods, stored in collections, and compared
2. **Memory allocation** depends on card implementation; complex cards with many properties benefit from heap allocation
3. **Nullability** is meaningful for optional cards (e.g., unknown opponent cards)
4. **Boxing avoidance** - struct cards in generic collections would box

### Type Hierarchy

```
IDeck<TCard>
├── FrenchDeck (abstract)
│   ├── FullFrenchDeck (52 cards)
│   └── ShortFrenchDeck (36 cards)
└── [Other deck implementations]

Dealer<TCard>
├── FrenchDeckDealer
└── [Other dealer implementations]
```

## Consequences

### Positive

- **Flexibility** - Any card type can be used with core abstractions
- **Type safety** - Compile-time checks prevent mixing card types
- **Code reuse** - Dealer, shuffle, and deal logic shared across games
- **Performance** - Reference semantics avoid copying large card objects
- **Extensibility** - New card games can reuse infrastructure

### Negative

- **Complexity** - Generic types add cognitive overhead
- **Null handling** - Must handle null for reference types
- **GC pressure** - More heap allocations than struct-based design

### Neutral

- **Learning curve** - Developers must understand generic constraints
- **Testing** - Need test implementations (TestCard, TestDeck)

## Implementation Details

### French Card Implementation

```csharp
public sealed class Card : IEquatable<Card>
{
    public Suit Suit { get; }
    public Symbol Symbol { get; }
    public int Value => (int)Symbol;
    
    public Card(Suit suit, Symbol symbol) { ... }
    public Card(Suit suit, int value) { ... }
    
    // Value equality
    public bool Equals(Card? other) => 
        other is not null && Suit == other.Suit && Symbol == other.Symbol;
}
```

### Deck Implementation Pattern

```csharp
public abstract class FrenchDeck : IDeck<Card>
{
    private readonly List<Card> _remainingCards = new();
    
    protected abstract IReadOnlyCollection<Card> AllCards();
    
    public int NumberOfCardsLeft() => _remainingCards.Count;
    
    public Card GetFromRemaining(int index)
    {
        var card = _remainingCards[index];
        _remainingCards.RemoveAt(index);
        return card;
    }
    
    public void Reset() => _remainingCards = AllCards().ToList();
}
```

### Dealer Pattern

```csharp
public class FrenchDeckDealer : Dealer<Card>
{
    public static FrenchDeckDealer WithFullDeck() => 
        new(new FullFrenchDeck());
    
    public static FrenchDeckDealer WithShortDeck() => 
        new(new ShortFrenchDeck());
    
    // Specialized methods using deck knowledge
    public bool TryDealCardOfSuit(Suit suit, out Card? card) { ... }
    public bool TryDealCardOfValue(int value, out Card? card) { ... }
}
```

## Alternatives Considered

### 1. Struct Constraint (`where TCard : struct`)

**Rejected because:**
- Cards are passed around frequently; copying is expensive
- Complex cards with multiple properties don't fit struct guidance
- Collections would box structs
- Originally used but switched after benchmarking

### 2. Interface Constraint (`where TCard : ICard`)

**Rejected because:**
- Forces all card types to implement specific interface
- Limits flexibility for simple card types
- Interface dispatch overhead

### 3. No Constraint (Object-based)

**Rejected because:**
- Loses compile-time type safety
- Requires runtime type checks
- No IDE support for card-specific members

### 4. Sealed Generic Type (`Card<TSuit, TValue>`)

**Rejected because:**
- Overly complex for most use cases
- Limits card representation options
- Harder to read and understand

## Performance Considerations

Benchmarking showed:

| Scenario | Struct Cards | Class Cards |
|----------|--------------|-------------|
| Deal 1000 hands | Faster initial creation | Faster passing |
| Store in collections | Boxing overhead | No boxing |
| Compare hands | Similar | Similar |
| Overall (simulation) | Slower | Faster |

For poker simulations that deal thousands of hands with hand evaluation, class-based cards performed better overall.

## References

- [Choosing Between Class and Struct - Microsoft](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/choosing-between-class-and-struct)
- [Generic Constraints - Microsoft](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters)
