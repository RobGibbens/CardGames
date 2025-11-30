# CardGames
- [Introduction](#introduction)
- [CardGames.Core](#cardgamescore)
  * [Overview](#overview)
  * [Card](#card)
  * [Deck](#deck)
  * [Dealer](#dealer)
- [CardGames.Core.French](#cardgamescorefrench)
- [CardGames.Poker](#cardgamespoker)
- [Benchmarks](#benchmarks)
- [License](#license)
- [Versioning](#versioning)
- [Feedback and Contributing](#feedback-and-contributing)

[![CI Build](https://github.com/EluciusFTW/CardGames/actions/workflows/CI.yml/badge.svg)](https://github.com/EluciusFTW/CardGames/actions/workflows/CI.yml)

# Introduction
After writing simulations and tools for card games (poker in particular) in the last years, I have decided to take a step back, sort through the code bases, clean up here and there, and distill out small, reusable packages that hopefully can be useful for the open source community.

### About the project
I assume this project will be slow, because of several reasons. First of all, it is a _pure leisure project_. Secondly, because I value design. I try to model the entities and their apis as closely as possible to the real world concepts they represent (albeit adding some convenience apis if they are very useful). I value code quality and readability a lot as well. I'll spend lots of time rewriting algorithmically simple things if I feel I can express them even cleaner, or more performantly. 

# CardGames.Core
Available at Nuget: [EluciusFTW.CardGames.Core](https://www.nuget.org/packages/EluciusFTW.CardGames.Core/)

### Overview
There are thousands of different [card games](https://en.wikipedia.org/wiki/Card_game), with many different cards and collections of cards, with different rules and purposes. 

In this package, we tried to follow a domain-driven approach to card games in general<sup>1</sup>. 
It contains the basic entities needed for such a game: _Cards_ (duh!), _Card decks_ (which are the finite collections of all possible cards of given type) and _Dealers_.

<sup>1</sup> Actually, up to abuse of language, any game of chance that contains a finite set of possibilities can be modelled using this package, e.g., we can interpret a die as a deck of six cards called: 1,2,3,4,5,6. We can implement a `DiceDealer` who 'shuffles the deck' (i.e. returns the dealt/rolled card/value back to the deck immediately) after dealing a card (rolling the die).

### Card
The most elemental part of a card game is the card. There is actually nothing universal that describes a card, except for it being detemined by it's content. So in all later entities the card will be represented by a generic type `TCard`, with the constraint that it is a **class**. 

> NOTE: Until recently, `TCard` was generically constrained to be a **struct**. However, I have decided to switch over to **class**, as in all my use-cases, the gain from passing-by-reference as default (in terms of performance) was higher than the benefit of creation on the stack, as they are often passed around into other methods, collections, etc.  Another reason is that the memory allocation needed to create an instance depends heavily on implementation, and might be big, depending on your card deck and game (think: Cards in a deck builder game with lot's of properties).

### Deck
The finite collection of all different cards, in a bunch, is called a deck.

The package provides a generic interface for a deck which holds cards of the generic type `TCard`
```cs
// Interface definition for a generic deck
public interface IDeck<TCard> where TCard : class
{
    int NumberOfCardsLeft();
    TCard GetFromRemaining(int index);
    TCard GetSpecific(TCard specificCard);
    void Reset();
};
````

### Dealer
The dealer is the entity handling the deck. Instead of only providing an interface like for the deck, the library provides a generic implementation of a dealer, which can be specified (i.e., derived from) in order to add more specific functionality:
```cs
public class Dealer<TCard> where TCard : class
{
    public Dealer(IDeck<TCard> deck) {...}
    public Dealer(IDeck<TCard> deck, IRandomNumberGenerator numberGenerator) {...}

    public TCard DealCard() {...}
    public IReadOnlyCollection<TCard> DealCards(int amount) {...}
    public void Shuffle() {...}
}
````
The dealer can deal one or many cards at once, and shuffle the deck. In order to do that, he needs a deck (duh!). In order to shuffle, he needs some source of randomness. We provide an interface you can implement,
```cs
public interface IRandomNumberGenerator
{
    public int Next(int upperBound);
}
````
and a standand implementation (which is just a wrapper holding an instance of `System.Random`).

# CardGames.Core.French
Available at Nuget: [EluciusFTW.CardGames.Core.French](https://www.nuget.org/packages/EluciusFTW.CardGames.Core.French/)

This library is an implementation of the core library for the arguably the most well-known playing card: the [french-suited playing card](https://en.wikipedia.org/wiki/French-suited_playing_cards).

### French-suited playing cards
A french-suited playing card is characterized by two properties: The _suit_ (Diamonds, Hearts, Clubs and Spades) and the _symbol_ (Deuce, Three ... King, Ace), both of which are represented as enums in the library. The _value_ of the card is a numeric value betwen 2 and 14 in bijective relation to it's symbol:
```cs
// Using the Suit and Symbol enum
var card = new Card(Suit.Hearts, Symbol.Deuce);

// Using the Value instead of the Symbol
var card = new Card(Suit.Hearts, 8);
````

### Serialization and Deserialization
There are conventional string representations for these cards, which we support via extensions (resp. implementing the `ToString()` method):
```cs
// String extension expecting the format {symbol char}{suit char}
var card = "Jc".ToCard();
var serializedCard = card.ToString() // equals "Jc" again.

// String extension expecting one or more cards separated by a space
var cards = "2h 5d Qs".ToCards();
var serializedCards = cards.ToStringRepresentation(); // equals "2h 5d Qs" again.
````

### Dealing with collections of cards
Since most card games involve players having more than one card, handling collections of cards is needed. We provide several extensions on `IEnumerable<Card>` for convenience:

```cs
// Get cards by descending value
IReadOnlyCollection<Card> ByDescendingValue(this IEnumerable<Card> cards)

// Get values in several flavors
IReadOnlyCollection<int> Values(this IEnumerable<Card> cards)
IReadOnlyCollection<int> DescendingValues(this IEnumerable<Card> cards)
IReadOnlyCollection<int> DistinctDescendingValues(this IEnumerable<Card> cards)

// Get all distinct suits
IReadOnlyCollection<Suit> Suits(this IEnumerable<Card> cards)

// Check if given values are all in the cards
bool ContainsValue(this IEnumerable<Card> cards, int value)
bool ContainsValues(this IEnumerable<Card> cards, IEnumerable<int> valuesToContain)

// Detemines highest value-duplicates
int ValueOfBiggestPair(this IEnumerable<Card> cards) 
int ValueOfBiggestTrips(this IEnumerable<Card> cards)
int ValueOfBiggestQuads(this IEnumerable<Card> cards)
````

## French decks
Of course, as we have provided the french-suited card, we also provide some decks containing these cards in this library.

First of all, there is a base class which provides a few more useful methods already using the fact that a french card has a `symbol` (resp. `value`) and a `suit`. The only thing an implementing class must provide is the collection of _all cards_ in the deck:
```cs
public abstract class FrenchDeck : IDeck<Cards.French.Card>
{
    protected abstract IReadOnlyCollection<Card> Cards();
    public IReadOnlyCollection<Card> CardsLeft() {...}
    public IReadOnlyCollection<Card> CardsLeftOfValue(int value) {...}
    public IReadOnlyCollection<Card> CardsLeftOfSuit(Suit suit) {...}
    public IReadOnlyCollection<Card> CardsLeftWith(Func<Card, bool> predicate) {...}
}
````
There are two implementations in the package:
- `FullFrenchDeck`: The standard 52-card deck consisting of Deuce-to-Ace of all four suits.
- `ShortFrenchDeck`: A 36-card deck consisting of Six-to-Ace of all four suits (like is used in [Short-deck poker](https://en.wikipedia.org/wiki/Six-plus_hold_%27em)).

## French-deck dealer
One could wonder why a dealer cares whyt kind of deck he deals, i.e., why a specific dealer implementation for a given deck makes sense.

In our domain view, the dealer is the owner of the deck, and responsible for dealing the cards. He hence has knowledge about the deck (a dealer can peek if he wants!), and using this knowledge, combined with a specific deck let's us add convenience methods on the dealer. 

The `FrenchDeckDealer` we provide in this package (including two factory methods for the decks we have defined earlier), can peek into the deck and try to narrow down the cards from which he deals the next, randomly:
```cs
// provides a dealer with full deck (or use .WithShortDeck() for a short deck)
var dealer = FrenchDeckDealer.WithfullDeck();

// deals a random card of given value, suit or symbol. 
// Succeeds if there are still some in the deck, else fails.
_ = dealer.TryDealCardOfValue(7, out var card);
_ = dealer.TryDealCardOfSymbol(Symbol.King, out var card);
_ = dealer.TryDealCardOfSuit(Suit.Spades, out var card);
````
This is very useful and increases performance in simulation scenarios where certain specific situations have to be recreated over and over.

# CardGames.Poker
This library utilizes the _French cards_ and proceeds to model Poker variants. It is not yet published as a package as it is not yet mature enough.

## Texas Hold 'Em (Complete Implementation)

The library includes a full implementation of Texas Hold 'Em, the most popular poker variant. The implementation includes:

### Game Orchestration

The `HoldEmGame` class provides complete game orchestration for Texas Hold 'Em:

```cs
// Create a game with players and blinds
var players = new[] { ("Alice", 1000), ("Bob", 1000), ("Charlie", 1000) };
var game = new HoldEmGame(players, smallBlind: 5, bigBlind: 10);

// Start a hand
game.StartHand();
game.CollectBlinds();
game.DealHoleCards();
game.StartPreFlopBettingRound();

// Process betting actions
game.ProcessBettingAction(BettingActionType.Call);
game.ProcessBettingAction(BettingActionType.Check);

// Deal community cards and continue
game.DealFlop();
game.StartPostFlopBettingRound();
// ... continue through Turn, River, and Showdown
```

### Game Phases

The game proceeds through well-defined phases:
1. **WaitingToStart** - Initial state
2. **CollectingBlinds** - Posting small and big blinds
3. **Dealing** - Dealing 2 hole cards per player
4. **PreFlop** - Pre-flop betting round
5. **Flop** - Flop betting round (after 3 community cards)
6. **Turn** - Turn betting round (after 4th community card)
7. **River** - River betting round (after 5th community card)
8. **Showdown** - Comparing hands and awarding pot
9. **Complete** - Hand is finished

### Betting System

Full betting support including:
- **No Limit** - Any bet size up to all chips
- **Fixed Limit** - Predetermined bet sizes
- **Pot Limit** - Maximum bet is current pot size

### Pot Management

The `PotManager` handles complex pot situations including:
- Main pot and side pot calculations
- All-in scenarios with multiple players
- Split pots for tied hands

### Hand Evaluation

The `HoldemHand` class automatically evaluates the best 5-card hand from 2 hole cards and 5 community cards:

```cs
var hand = new HoldemHand(holeCards, communityCards);
Console.WriteLine(hand.Type);      // e.g., HandType.Flush
Console.WriteLine(hand.Strength);  // Numeric strength for comparison
```

### Variant Registration

Hold 'Em is registered via the variant engine:

```cs
var registry = new GameVariantRegistry();
var info = new GameVariantInfo(
    Id: "texas-holdem",
    Name: "Texas Hold'em",
    Description: "The most popular poker variant.",
    MinPlayers: 2,
    MaxPlayers: 10);

registry.RegisterVariant(info, (players, sb, bb) => 
    new HoldEmGame(players, sb, bb));
```

### Predefined Rules

The `PredefinedRuleSets.TexasHoldem` provides the canonical ruleset:
- 52-card deck
- 2 hole cards (use 0-2 in final hand)
- 5 community cards (use 0-5 in final hand)
- Small blind + Big blind structure
- Last aggressor shows first at showdown

## Omaha (Complete Implementation)

The library includes a full implementation of Omaha poker, another popular poker variant. The key difference from Texas Hold 'Em is that players receive 4 hole cards and must use exactly 2 of them with exactly 3 community cards to make their best hand.

### Game Orchestration

The `OmahaGame` class provides complete game orchestration for Omaha:

```cs
// Create a game with players and blinds
var players = new[] { ("Alice", 1000), ("Bob", 1000), ("Charlie", 1000) };
var game = new OmahaGame(players, smallBlind: 5, bigBlind: 10);

// Start a hand
game.StartHand();
game.PostBlinds();
game.DealHoleCards();  // Each player receives 4 hole cards
game.StartBettingRound();

// Process betting actions
game.ProcessBettingAction(BettingActionType.Call);
game.ProcessBettingAction(BettingActionType.Check);

// Deal community cards and continue
game.DealFlop();
game.StartBettingRound();
// ... continue through Turn, River, and Showdown
```

### Game Phases

The game proceeds through well-defined phases:
1. **WaitingToStart** - Initial state
2. **PostingBlinds** - Posting small and big blinds
3. **Preflop** - Pre-flop betting round (after dealing 4 hole cards per player)
4. **Flop** - Flop betting round (after 3 community cards)
5. **Turn** - Turn betting round (after 4th community card)
6. **River** - River betting round (after 5th community card)
7. **Showdown** - Comparing hands and awarding pot
8. **Complete** - Hand is finished

### Omaha-Specific Rules

The critical Omaha rule is enforced automatically:
- Players **must use exactly 2 hole cards** from their 4 hole cards
- Players **must use exactly 3 community cards** from the 5 community cards
- This creates a 5-card hand

### Hand Evaluation

The `OmahaHand` class automatically evaluates the best 5-card hand following Omaha rules:

```cs
var hand = new OmahaHand(holeCards, communityCards);  // holeCards must have 4 cards
Console.WriteLine(hand.Type);      // e.g., HandType.Flush
Console.WriteLine(hand.Strength);  // Numeric strength for comparison
```

### Variant Registration

Omaha is registered via the variant engine:

```cs
var registry = new GameVariantRegistry();
var info = new GameVariantInfo(
    Id: "omaha",
    Name: "Omaha",
    Description: "Omaha poker - use exactly 2 hole cards and 3 community cards.",
    MinPlayers: 2,
    MaxPlayers: 10);

registry.RegisterVariant(info, (players, sb, bb) => 
    new OmahaGame(players, sb, bb));
```

### Predefined Rules

The `PredefinedRuleSets.Omaha` provides the canonical ruleset:
- 52-card deck
- 4 hole cards (must use exactly 2 in final hand)
- 5 community cards (must use exactly 3 in final hand)
- Small blind + Big blind structure
- Pot Limit is the most common betting structure (PLO)
- Last aggressor shows first at showdown

## Seven Card Stud (Complete Implementation)

The library includes a full implementation of Seven Card Stud, a classic stud poker variant. Unlike Hold 'Em and Omaha, Seven Card Stud does not use community cards - each player receives their own 7 cards (3 face-down, 4 face-up) and makes the best 5-card hand.

### Game Orchestration

The `SevenCardStudGame` class provides complete game orchestration for Seven Card Stud:

```cs
// Create a game with players and betting structure
var players = new[] { ("Alice", 1000), ("Bob", 1000), ("Charlie", 1000) };
var game = new SevenCardStudGame(
    players, 
    ante: 5, 
    bringIn: 10, 
    smallBet: 20, 
    bigBet: 40, 
    useBringIn: true);

// Start a hand
game.StartHand();
game.CollectAntes();
game.DealThirdStreet();
game.PostBringIn();
game.StartBettingRound();

// Process betting actions
game.ProcessBettingAction(BettingActionType.Call);
game.ProcessBettingAction(BettingActionType.Check);

// Deal more streets and continue betting
game.DealStreetCard();  // Fourth Street
game.StartBettingRound();
// ... continue through Fifth, Sixth, Seventh Street and Showdown
```

### Game Phases

The game proceeds through well-defined phases:
1. **WaitingToStart** - Initial state
2. **CollectingAntes** - Collecting antes from all players
3. **ThirdStreet** - Deal 2 down cards + 1 up card, bring-in betting round
4. **FourthStreet** - Deal 1 up card, betting round (small bet)
5. **FifthStreet** - Deal 1 up card, betting round (big bet)
6. **SixthStreet** - Deal 1 up card, betting round (big bet)
7. **SeventhStreet** - Deal 1 down card, final betting round (big bet)
8. **Showdown** - Comparing hands and awarding pot
9. **Complete** - Hand is finished

### Stud-Specific Rules

Seven Card Stud has unique rules that differ from community card games:
- **Ante Structure** - All players post an ante before the hand
- **Bring-In** - The player with the lowest upcard posts a forced bring-in bet
- **Visible Cards** - Players can see each other's up cards, affecting strategy
- **Best Visible Hand** - After third street, the player with the best visible hand acts first
- **Player Limit** - Maximum 7 players due to card constraints (7 × 7 = 49 cards)

### Hand Evaluation

The `SevenCardStudHand` class evaluates the best 5-card hand from 7 cards:

```cs
var hand = new SevenCardStudHand(holeCards, boardCards, downCard);
Console.WriteLine(hand.Type);      // e.g., HandType.Flush
Console.WriteLine(hand.Strength);  // Numeric strength for comparison
```

### Variant Registration

Seven Card Stud is registered via the variant engine:

```cs
var registry = new GameVariantRegistry();
var info = new GameVariantInfo(
    Id: "seven-card-stud",
    Name: "Seven Card Stud",
    Description: "A classic stud poker variant.",
    MinPlayers: 2,
    MaxPlayers: 7);

registry.RegisterVariant(info, (players, sb, bb) => 
    new SevenCardStudGame(players, ante: sb, bringIn: bb / 2, smallBet: bb, bigBet: bb * 2, useBringIn: true));
```

### Predefined Rules

The `PredefinedRuleSets.SevenCardStud` provides the canonical ruleset:
- 52-card deck
- 7 cards dealt (3 face-down, 4 face-up)
- Must use exactly 5 cards in final hand
- Ante + bring-in structure (no blinds)
- Fixed limit betting (most common)
- Maximum 7 players
- Last aggressor shows first at showdown

## Hands
The library contains domain models for hands in these poker disciplines:
 - 5-card draw
 - Holdem 
 - Omaha
 - Stud 

The Holdem and Omaha hands derive from a more generic hand model called `CommunityCardsHand`, which can be used to model any kind of community card hand (any number of community cards, any number of hole cards, any requirement how many of them have to be used for a hand, and how many must at least be used. So in these parameters, a Holdem hand is (3-5, 2, 0, 2) and a Omaha hand (3-5, 4, 2, 2)). So using this as a basis, it is easy to implement, e.g., 5-card PLO and other lesser known variants.

Hands all implement `IComparable`, and the operators "<, >" are implemented by default. This is accompished by using two properties of the base class of any hand:  
Strength (of type `long`) and Type (e.g. `HandType.Flush`). The calculations of the strength and type are directly performed when constructing the hand, and they are designed in such a fashion that the ordering of the types can be provided as well (because, e.g., in short deck, a flush beats a full-house). The classical orderign as well as the ordering for short-deck poker are provided in the class `HandTypeStrength`.

## Simulations
The library contains models for Holdem (full and short deck) and Stud simulations (currently still in the `CardGames.Playground` project, but they will soon move to the `CardGames.Poker` project), and other simulations can easily be built in similar fashion. Both Simulations are configurable with a fluent builder pattern. Here's an example of a Holdem simulation configuration:
```cs
// any number of players can be added
// each players hole cards can be specified by providing zero, one or two cards
// optionally, a flop/turn/river can be provided
// finally the simulation is executed by calling SimulateWithFullDeck resp. SimulateWithShortDeck
private HoldemSimulationResult RunHoldemSimulation(int nrOfHAnds)
    => new HoldemSimulation()
        .WithPlayer("John", "Js Jd".ToCards())
        .WithPlayer("Jeremy", "8s 6d".ToCards())
        .WithPlayer("Jarvis", "Ad".ToCards()) 
        .WithFlop("8d 8h 4d".ToCards()) 
        .SimulateWithFullDeck(nrOfHAnds);
````

The Stud simulation works similarly. However, since a player has different kinds of cards, one provides any number of `StudPlayers` to the simulation, which have a builder of their own. Here's what that looks like in an example:
````cs
// again, any number of players can be specified
// and each players hole and board cards can be specified individually
private StudSimulationResult RunStudSimulation(int nrOfHAnds)
    => new SevenCardStudSimulation()
        .WithPlayer(
            new StudPlayer("John")
                .WithHoleCards("Js Jd".ToCards())
                .WithBoardCards("Qc".ToCards()))
        .WithPlayer(
            new StudPlayer("Jeremy")
                .WithHoleCards("3s 4s".ToCards())
                .WithBoardCards("7s".ToCards()))
        .WithPlayer(
            new StudPlayer("Jarvis")
                .WithBoardCards("Tc".ToCards()))
        .Simulate(nrOfHAnds);
````

If you want to play around with these simulations, there is a console program in `CardGames.Playground.Runner` where you can run any simulation. It also contains some benchmarks (using [BenchmarkDotNet](https://benchmarkdotnet.org/)), which you can run. In fact, they have been instrumental in finding the right balance between design and performance, big shoutout to them!

The simulation result classes contain a complete collection of all run hands, as well as some predefined queries and aggregations, which can easily be extendend and customized due to the fact that the full collection of hands is available. 

Here is a simple printout of the above Stud simulation:
![Screenshot of Stud Simulation](./sample/stud-simulation-screenshot.png)

Here is a simple printout of the above Holdem simulation:
![Screenshot of Holdem Simulation](./sample/holdem-simulation-screenshot.png)

## Benchmarks
This repository also contians two benchmark projects: one for the core packages, one for the poker simulations. These are only meant to be utilities during development in order to test the implementations for their performance, resp. to prevent introduction of performance regressions. Once stable enough, baseline benchmarks _might be included_ in the documentation and in the workflows.

## Versioning
We have switched from manual semantic versioning to using [NerdBank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) and follow the version scheme: `<major>.<minor>.<git-depth>` for out releases. All packages in this repository will have synchronized version numbers.

> <b>Note</b>: In particular, the _third number_ in the version does not have the same meaning as the patches in SemVer. Increments in that number may contain breaking changes, in contrast to patch versions in SemVer.

## Feedback and Contributing
All feedback welcome!
All contributions are welcome!
