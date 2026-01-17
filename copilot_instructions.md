# CardGames Project - Copilot Instructions

## Project Overview

This is a domain-driven card game simulation library with a focus on **design quality**, **performance**, and **clean architecture**. The codebase models real-world card game concepts through well-abstracted generic types and domain-specific implementations.

### Core Philosophy
- **Design over speed**: We value modeling entities as closely as possible to real-world concepts
- **Code quality**: Readability, maintainability, and expressiveness are paramount
- **Performance awareness**: Design decisions are performance-tested using BenchmarkDotNet
- **Pure leisure project**: Quality and thoughtfulness over velocity

## Project Structure

### Solution Hierarchy
```
CardGames/
├── CardGames.Core/              # Generic card game abstractions (.NET Standard 2.1)
├── CardGames.Core.French/       # French-suited playing cards (.NET Standard 2.1)
├── CardGames.Poker/             # Poker variants and simulations (.NET 8)
├── CardGames.Poker.CLI/         # Console interface for poker simulations (.NET 8)
├── CardGames.Core.Benchmarks/   # Performance benchmarks for core
├── CardGames.Poker.Benchmarks/  # Performance benchmarks for poker
└── Tests/
    ├── CardGames.Core.Tests/
    ├── CardGames.Core.French.Tests/
    └── CardGames.Poker.Tests/
```

### Target Frameworks
- **Libraries**: .NET Standard 2.1 (for maximum compatibility)
- **Applications/Tests**: .NET 8
- **Never modify** TFM or SDK versions unless explicitly requested

## Domain Model Principles

### Generic Card Type Architecture
- Generic type `TCard` is constrained to `class` (not `struct`)
- **Rationale**: Reference semantics provide better performance when cards are passed around, stored in collections
- Cards are passed by reference to avoid stack allocation overhead for complex card types

### Naming Conventions
- Generic type parameter for cards: `TCard` or `TCardKind` (use consistently within a file)
- Use domain terminology: `Dealer`, `Deck`, `Shuffle`, `DealCard` (not technical jargon)
- Keep naming patterns consistent (e.g., if you use `WithHostPort`, use `WithBrowserPort`, not `AndBrowserPort`)

### Core Abstractions
```csharp
// Deck interface - finite collection of all possible cards
public interface IDeck<TCard> where TCard : class
{
    int NumberOfCardsLeft();
    TCard GetFromRemaining(int index);
    TCard GetSpecific(TCard specificCard);
    void Reset();
}

// Dealer - handles the deck and card distribution
public class Dealer<TCard> where TCard : class
{
    protected IDeck<TCard> Deck { get; }
    protected IRandomNumberGenerator NumberGenerator { get; }
    // ...
}
```

## Code Style & Standards

### General C# Conventions
- **Namespace organization**: File-scoped namespaces (`namespace CardGames.Core.Dealer;`)
- **Modern C#**: Use latest language features appropriate for target framework
  - Records for DTOs
  - Pattern matching and switch expressions
  - Null-coalescing and null-conditional operators
  - Collection expressions (when targeting .NET 8+)
- **Immutability**: Prefer `IReadOnlyCollection<T>` over `IEnumerable<T>` for return types
- **LINQ**: Use fluent, readable LINQ expressions; avoid over-nesting

### Visibility & Access Modifiers
- **Default to least exposure**: `private` > `internal` > `protected` > `public`
- Only expose what's necessary for the public API
- Use `protected` for extensibility points in base classes

### Comments & Documentation
- Comments explain **why**, not **what**
- Avoid comments for self-explanatory code
- Keep comments consistent with existing style in the file
- No XML documentation unless it's a public API in a library project

### Equality & Comparison
When implementing value objects (like `Card`):
```csharp
public sealed class Card : IEquatable<Card>
{
    public override bool Equals(object obj) 
        => obj is Card other && Equals(other);

    public bool Equals(Card other) 
        => other is not null && /* equality logic */;

    public static bool operator ==(Card left, Card right)
        => left?.Equals(right) ?? false;

    public static bool operator !=(Card left, Card right) 
        => !(left == right);

    public override int GetHashCode() => /* consistent with Equals */;
}
```

### Extension Methods
- Place in dedicated `Extensions` folders
- Name files descriptively: `SerializationExtensions.cs`, `CardsExtensions.cs`
- Group related extensions together
- Common pattern: `IEnumerable<Card>` extensions for collection operations

### Error Handling
```csharp
// Validate arguments early
if (cards.Count < 5)
{
    throw new ArgumentException("A poker hand needs at least five cards");
}

// Use specific exception types
if (cardsLeft < 1)
{
    throw new InvalidOperationException("There are no more cards in the deck to deal.");
}

// Use null-conditional and coalescing operators
public static bool operator ==(Card left, Card right)
    => left?.Equals(right) ?? false;
```

## Domain-Specific Patterns

### Fluent Builder Pattern
Used extensively for simulations:
```csharp
var result = new HoldemSimulation()
    .WithPlayer("John", "Js Jd".ToCards())
    .WithPlayer("Jeremy", "8s 6d".ToCards())
    .WithFlop("8d 8h 4d".ToCards())
    .SimulateWithFullDeck(10000);
```

### Factory Methods
Prefer static factory methods over constructors for common configurations:
```csharp
// Good - clear intent
var dealer = FrenchDeckDealer.WithFullDeck();
var dealer = FrenchDeckDealer.WithShortDeck();

// Avoid - requires knowledge of implementation
var dealer = new FrenchDeckDealer(new FullFrenchDeck());
```

### Lazy Evaluation with Caching
```csharp
private long _strength;
public long Strength => _strength != default
    ? _strength
    : _strength = CalculateStrength();
```

### Comparison Operators
Implement `IComparable<T>` for domain objects that have natural ordering:
```csharp
public abstract class HandBase : IComparable<HandBase>
{
    public static bool operator >(HandBase thisHand, HandBase otherHand)
        => thisHand.CompareTo(otherHand) > 0;

    public static bool operator <(HandBase thisHand, HandBase otherHand)
        => thisHand.CompareTo(otherHand) < 0;

    public int CompareTo(HandBase other)
        => Strength.CompareTo(other.Strength);
}
```

## Testing Standards

### Framework & Tools
- **Test Framework**: xUnit
- **Assertions**: FluentAssertions
- **Mocking**: NSubstitute
- **Coverage**: coverlet.collector

### Test Structure
```csharp
public class DealerTests
{
    private readonly TestDealer _dealer;
    private readonly IDeck<TestCard> _deck;
    private readonly IRandomNumberGenerator _numberGenerator;

    public DealerTests()
    {
        _deck = Substitute.For<IDeck<TestCard>>();
        _numberGenerator = Substitute.For<IRandomNumberGenerator>();
        _dealer = new TestDealer(_deck, _numberGenerator);
    }

    [Fact]
    public void Shuffle_Resets_The_Deck()
    {
        // Arrange
        
        // Act
        _dealer.Shuffle();

        // Assert
        _deck.Received(1).Reset();
    }
}
```

### Test Conventions
- **Class naming**: `{ClassName}Tests`
- **Method naming**: `{Method}_{Scenario}` or `{Behavior}_When_{Condition}`
- Use `[Fact]` for simple tests, `[Theory]` with `[InlineData]` for parameterized tests
- One assert per test (or use `Should().And.` chain)
- Arrange-Act-Assert pattern
- Initialize dependencies in constructor
- Use test implementations (e.g., `TestCard`, `TestDealer`) for abstract/generic types

### Mocking with NSubstitute
```csharp
// Setup return values
_deck.NumberOfCardsLeft().Returns(15);
_numberGenerator.Next(15).Returns(12);

// Setup parameterized returns
_deck.GetFromRemaining(Arg.Any<int>())
    .Returns(x => new TestCard((int)x[0]));

// Verify calls
_deck.Received(1).Reset();
```

### Test Coverage
Run coverage before committing new tests:
```bash
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

## Performance Considerations

### Benchmark Projects
- Use BenchmarkDotNet for performance testing
- Run benchmarks when:
  - Optimizing hot paths
  - Comparing algorithmic approaches
  - Validating design decisions don't regress performance
  - Major refactoring

### Performance Patterns
- **Avoid allocations in hot paths**: Use `Span<T>`, `Memory<T>`, array pooling where measured to help
- **Lazy evaluation**: Cache expensive calculations (see Strength/Type in `HandBase`)
- **LINQ is acceptable**: Readability over micro-optimizations unless measured
- **Measure first, optimize second**: "Simple first; optimize hot paths when measured"

## Specific Module Guidelines

### CardGames.Core
- Pure abstractions, no concrete card types
- Generic type `TCard where TCard : class`
- Must support any card game (even dice games can be modeled as cards)
- No dependencies beyond .NET Standard 2.1

### CardGames.Core.French
- Concrete implementation for French-suited cards
- Two enums: `Suit` (Diamonds, Hearts, Clubs, Spades), `Symbol` (Deuce through Ace)
- `Card.Value` is int (2-14) corresponding to Symbol
- String serialization format: `{symbol char}{suit char}` (e.g., "Jc", "2h")
- Extensive collection extensions for `IEnumerable<Card>`
- Two deck types: `FullFrenchDeck` (52 cards), `ShortFrenchDeck` (36 cards, Six-Ace)

### CardGames.Poker
- Targets .NET 8
- Hand types: 5-card draw, Holdem, Omaha, Stud
- All hands implement `IComparable<HandBase>`
- `HandTypeStrengthRanking` allows different orderings (classic vs short-deck)
- Simulations use fluent builder pattern
- Performance-critical: hand evaluation happens during simulation

## Common Patterns to Follow

### String Serialization
```csharp
// Extension method for parsing
public static Card ToCard(this string cardString) { /* ... */ }
public static IReadOnlyCollection<Card> ToCards(this string cardsString) { /* ... */ }

// ToString() for serialization
public override string ToString() 
    => this.ToShortString();
```

### Dealing Cards
```csharp
// Single card
public TCard DealCard()
{
    var cardsLeft = Deck.NumberOfCardsLeft();
    if (cardsLeft < 1)
    {
        throw new InvalidOperationException("There are no more cards in the deck to deal.");
    }
    var nextCardPosition = NumberGenerator.Next(cardsLeft);
    return Deck.GetFromRemaining(nextCardPosition);
}

// Multiple cards
public IReadOnlyCollection<TCard> DealCards(int amount)
    => Enumerable
        .Repeat(1, amount)
        .Select(_ => DealCard())
        .ToList();
```

### Validation in Constructors
```csharp
public HandBase(IReadOnlyCollection<Card> cards)
{
    if (cards.Count < 5)
    {
        throw new ArgumentException("A poker hand needs at least five cards");
    }
    Cards = cards;
}
```

## NuGet Package Conventions

### Package Metadata (for libraries only)
- **PackageId**: `EluciusFTW.CardGames.{ProjectName}`
- **License**: GPL-3.0-or-later
- **Authors**: Guy Buss
- **Include symbols**: `<IncludeSymbols>true</IncludeSymbols>`
- **Symbol format**: snupkg

### Versioning
- Uses **Nerdbank.GitVersioning**
- Version scheme: `<major>.<minor>.<git-depth>`
- Third number is NOT semantic versioning patch (may contain breaking changes)
- All packages synchronized to same version

## Anti-Patterns to Avoid

### Don't
- ❌ Add interfaces/abstractions unless used for external dependencies or testing
- ❌ Wrap existing abstractions unnecessarily
- ❌ Default to `public` visibility
- ❌ Edit auto-generated code (`*.g.cs`, `// <auto-generated>`)
- ❌ Add unused methods or parameters
- ❌ Use synchronous-over-async patterns
- ❌ Change TFM, SDK, or LangVersion without explicit request
- ❌ Add XML doc comments for internal/private members
- ❌ Add comments that restate what the code obviously does
- ❌ Create overly generic abstractions "just in case"

### Do
- ✅ Model domain concepts accurately
- ✅ Keep names consistent within a file/feature
- ✅ Check sibling methods when fixing one method (similar bugs may exist)
- ✅ Reuse existing methods as much as possible
- ✅ Follow the project's own conventions first, then .NET conventions
- ✅ Use modern C# features appropriate for the target framework
- ✅ Write tests for new public APIs
- ✅ Run tests and ensure they pass before considering work complete

## Code Review Checklist

Before submitting changes:
1. ✅ Follows existing naming conventions in the file/project
2. ✅ Uses appropriate visibility modifiers (least exposure principle)
3. ✅ No unused using directives or variables
4. ✅ Consistent with domain terminology
5. ✅ Tests added/updated for public API changes
6. ✅ Error handling uses specific exception types
7. ✅ LINQ queries are readable and maintainable
8. ✅ No synchronous-over-async anti-patterns
9. ✅ Generic constraints match project patterns (`where TCard : class`)
10. ✅ Comments explain "why" not "what"

## Build & Test Commands

```bash
# Build solution
dotnet build

# Run all tests
dotnet test

# Run tests with coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test

# Run benchmarks (from benchmark project directory)
dotnet run -c Release

# Publish a library
dotnet pack -c Release
```

## Additional Resources

- **Project Repository**: https://github.com/EluciusFTW/CardGames
- **NuGet Packages**:
  - EluciusFTW.CardGames.Core
  - EluciusFTW.CardGames.Core.French
- **Benchmark Tool**: BenchmarkDotNet
- **Versioning**: Nerdbank.GitVersioning

## Questions to Ask Before Implementation

When receiving a task:
1. What domain concept am I modeling?
2. Should this be generic or specific?
3. What's the appropriate visibility level?
4. Does a similar pattern exist in the codebase?
5. Will this need to be extended/overridden?
6. Should I write tests for this?
7. Is this a performance-sensitive path that needs benchmarking?
8. What target framework am I working in?
