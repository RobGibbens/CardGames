# CardGames Architecture Documentation

This document outlines the proposed solution/project structure, folder layout, and dependencies for the CardGames Poker applications.

## Overview

The CardGames Poker solution consists of three main projects:

| Project | Description | Architecture |
|---------|-------------|--------------|
| **CardGames.Poker.Api** | Backend REST API | Vertical Slice Architecture with ASP.NET Minimal APIs, MediatR, CQRS |
| **CardGames.Poker.Web** | Frontend web application | ASP.NET 10 Blazor Server with Vertical Slice Architecture |
| **CardGames.Poker.Shared** | Shared contracts and DTOs | Class library referenced by both API and Web |

## Solution Structure

```
CardGames/
├── src/
│   ├── CardGames.Poker.Api/           # Backend API
│   ├── CardGames.Poker.Web/           # Blazor Server Frontend
│   ├── CardGames.Poker.Shared/        # Shared contracts/DTOs
│   ├── CardGames.AppHost/             # .NET Aspire orchestration
│   ├── CardGames.ServiceDefaults/     # Shared service configuration
│   └── ... (existing projects)
├── Tests/
│   ├── CardGames.Poker.Api.Tests/
│   └── CardGames.Poker.Web.Tests/
└── architecture/
    └── README.md                       # This file
```

---

## CardGames.Poker.Api

### Architecture: Vertical Slice Architecture

The API uses a **Vertical Slice Architecture** pattern, organizing code by feature rather than technical layers. This approach:
- Groups all code for a feature together (request, handler, response, validation)
- Reduces coupling between features
- Makes features easier to understand, test, and maintain
- Enables CQRS (Command Query Responsibility Segregation) naturally

### Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | ASP.NET 10 Minimal APIs |
| Mediator | MediatR |
| Database | SQL Server |
| ORM | Entity Framework Core |
| Validation | FluentValidation |
| API Documentation | OpenAPI/Swagger |

### Folder Structure

```
CardGames.Poker.Api/
├── Features/
│   ├── Games/
│   │   ├── CreateGame/
│   │   │   ├── CreateGameCommand.cs       # CQRS Command
│   │   │   ├── CreateGameHandler.cs       # MediatR Handler
│   │   │   ├── CreateGameValidator.cs     # FluentValidation
│   │   │   └── CreateGameEndpoint.cs      # Minimal API Endpoint
│   │   ├── GetGame/
│   │   │   ├── GetGameQuery.cs            # CQRS Query
│   │   │   ├── GetGameHandler.cs
│   │   │   └── GetGameEndpoint.cs
│   │   ├── ListGames/
│   │   │   ├── ListGamesQuery.cs
│   │   │   ├── ListGamesHandler.cs
│   │   │   └── ListGamesEndpoint.cs
│   │   └── GamesModule.cs                 # Feature module registration
│   ├── Players/
│   │   ├── CreatePlayer/
│   │   ├── GetPlayer/
│   │   └── PlayersModule.cs
│   ├── Hands/
│   │   ├── DealHand/
│   │   ├── EvaluateHand/
│   │   └── HandsModule.cs
│   └── Simulations/
│       ├── RunSimulation/
│       └── SimulationsModule.cs
├── Data/
│   ├── AppDbContext.cs                    # EF Core DbContext
│   ├── Configurations/                    # EF Core entity configurations
│   │   ├── GameConfiguration.cs
│   │   └── PlayerConfiguration.cs
│   └── Migrations/                        # EF Core migrations
├── Common/
│   ├── Behaviors/                         # MediatR pipeline behaviors
│   │   ├── ValidationBehavior.cs
│   │   └── LoggingBehavior.cs
│   └── Exceptions/
│       └── ValidationException.cs
├── Extensions/
│   ├── ServiceCollectionExtensions.cs     # DI registration
│   └── EndpointRouteBuilderExtensions.cs  # Endpoint mapping
├── Properties/
│   └── launchSettings.json
├── Program.cs                             # Application entry point
├── appsettings.json
├── appsettings.Development.json
└── CardGames.Poker.Api.csproj
```

### CQRS Pattern

Commands and Queries are separated for clear intent:

**Commands** (write operations):
- `CreateGameCommand` - Create a new poker game
- `JoinGameCommand` - Player joins a game
- `DealHandCommand` - Deal cards to players
- `PlaceBetCommand` - Player places a bet

**Queries** (read operations):
- `GetGameQuery` - Get game details
- `ListGamesQuery` - List available games
- `GetPlayerHandQuery` - Get player's current hand
- `GetSimulationResultsQuery` - Get simulation results

### Dependencies

```xml
<ItemGroup>
  <!-- ASP.NET Core -->
  <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
  
  <!-- MediatR for CQRS -->
  <PackageReference Include="MediatR" Version="12.4.1" />
  
  <!-- Validation -->
  <PackageReference Include="FluentValidation" Version="11.11.0" />
  <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />
  
  <!-- Entity Framework Core with SQL Server -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />
  
  <!-- Aspire Service Defaults -->
  <ProjectReference Include="..\CardGames.ServiceDefaults\CardGames.ServiceDefaults.csproj" />
  
  <!-- Shared contracts -->
  <ProjectReference Include="..\CardGames.Poker.Shared\CardGames.Poker.Shared.csproj" />
  
  <!-- Domain library -->
  <ProjectReference Include="..\CardGames.Poker\CardGames.Poker.csproj" />
</ItemGroup>
```

---

## CardGames.Poker.Web

### Architecture: Vertical Slice Architecture with Features

The Blazor Server application uses a **Feature-based Vertical Slice Architecture**, organizing components by feature/functionality rather than by component type.

### Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | ASP.NET 10 Blazor Server |
| State Management | Cascading Parameters / Fluxor (optional) |
| HTTP Client | Typed HttpClient with Refit (optional) |
| UI Components | Bootstrap / Custom Blazor Components |

### Folder Structure

```
CardGames.Poker.Web/
├── Features/
│   ├── Games/
│   │   ├── Components/
│   │   │   ├── GameList.razor              # Game listing component
│   │   │   ├── GameList.razor.cs           # Code-behind
│   │   │   ├── GameCard.razor              # Single game card
│   │   │   └── GameDetails.razor           # Game details view
│   │   ├── Pages/
│   │   │   ├── GamesPage.razor             # /games route
│   │   │   └── GamePage.razor              # /games/{id} route
│   │   └── Services/
│   │       └── GamesApiClient.cs           # API client for games
│   ├── Players/
│   │   ├── Components/
│   │   │   ├── PlayerProfile.razor
│   │   │   └── PlayerStats.razor
│   │   ├── Pages/
│   │   │   └── PlayerPage.razor
│   │   └── Services/
│   │       └── PlayersApiClient.cs
│   ├── Hands/
│   │   ├── Components/
│   │   │   ├── HandDisplay.razor           # Display poker hand
│   │   │   ├── CardComponent.razor         # Single card display
│   │   │   └── HandEvaluator.razor         # Hand evaluation display
│   │   ├── Pages/
│   │   │   └── HandAnalyzerPage.razor
│   │   └── Services/
│   │       └── HandsApiClient.cs
│   └── Simulations/
│       ├── Components/
│       │   ├── SimulationConfig.razor      # Simulation configuration
│       │   ├── SimulationResults.razor     # Results display
│       │   └── OddsCalculator.razor        # Odds calculator
│       ├── Pages/
│       │   └── SimulationsPage.razor
│       └── Services/
│           └── SimulationsApiClient.cs
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   ├── MainLayout.razor.css
│   │   ├── NavMenu.razor
│   │   └── NavMenu.razor.css
│   ├── App.razor
│   ├── Routes.razor
│   └── _Imports.razor
├── Shared/
│   ├── Components/                         # Reusable UI components
│   │   ├── LoadingSpinner.razor
│   │   ├── ErrorBoundary.razor
│   │   └── ConfirmDialog.razor
│   └── Services/
│       ├── ApiClientBase.cs                # Base HTTP client
│       └── NotificationService.cs
├── wwwroot/
│   ├── css/
│   │   ├── app.css
│   │   └── bootstrap/
│   ├── images/
│   │   └── cards/                          # Card images
│   └── favicon.png
├── Properties/
│   └── launchSettings.json
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
└── CardGames.Poker.Web.csproj
```

### Feature Organization

Each feature folder contains:
- **Components/**: Blazor components specific to this feature
- **Pages/**: Routable Blazor pages (with `@page` directive)
- **Services/**: Feature-specific services (API clients, state management)

### Dependencies

```xml
<ItemGroup>
  <!-- Aspire Service Defaults -->
  <ProjectReference Include="..\CardGames.ServiceDefaults\CardGames.ServiceDefaults.csproj" />
  
  <!-- Shared contracts -->
  <ProjectReference Include="..\CardGames.Poker.Shared\CardGames.Poker.Shared.csproj" />
</ItemGroup>
```

---

## CardGames.Poker.Shared

### Purpose

The Shared project contains contracts, DTOs, and common types used by both the API and Web projects. This ensures type safety and consistency across the solution.

### Folder Structure

```
CardGames.Poker.Shared/
├── Contracts/
│   ├── Requests/
│   │   ├── CreateGameRequest.cs
│   │   ├── JoinGameRequest.cs
│   │   ├── DealHandRequest.cs
│   │   └── RunSimulationRequest.cs
│   └── Responses/
│       ├── GameResponse.cs
│       ├── PlayerResponse.cs
│       ├── HandResponse.cs
│       └── SimulationResultResponse.cs
├── DTOs/
│   ├── CardDto.cs
│   ├── HandDto.cs
│   ├── PlayerDto.cs
│   ├── GameDto.cs
│   └── RuleSets/
│       └── RuleSetDto.cs          # Variant ruleset schema
├── Enums/
│   ├── GameState.cs
│   ├── HandType.cs
│   ├── PokerVariant.cs
│   ├── DeckType.cs
│   ├── LimitType.cs
│   └── ShowdownOrder.cs
├── RuleSets/
│   └── PredefinedRuleSets.cs      # Hold'em & Omaha definitions
├── Serialization/
│   └── RuleSetSerializer.cs       # JSON serialization
├── Validation/
│   └── RuleSetValidator.cs        # Configuration validation
└── CardGames.Poker.Shared.csproj
```

### Dependencies

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

---

## Variant Rules Schema

The CardGames solution includes a canonical, versioned ruleset schema for representing poker variant configurations. This schema enables flexible game configuration while maintaining type safety and validation.

### Schema Version

Current schema version: **1.0**

The schema follows semantic versioning. Minor version bumps (1.x) maintain backward compatibility.

### RuleSetDto Structure

The `RuleSetDto` is the root type for variant configuration:

| Property | Type | Description |
|----------|------|-------------|
| `schemaVersion` | string | Schema version (e.g., "1.0") |
| `id` | string | Unique ruleset identifier |
| `name` | string | Display name of the variant |
| `variant` | PokerVariant | Enum value for the variant type |
| `deckComposition` | DeckCompositionDto | Deck configuration |
| `cardVisibility` | CardVisibilityDto | Card visibility rules |
| `bettingRounds` | BettingRoundDto[] | Betting round configuration |
| `holeCardRules` | HoleCardRulesDto | Hole card rules |
| `communityCardRules` | CommunityCardRulesDto? | Community card rules (optional) |
| `anteBlindRules` | AnteBlindRulesDto? | Ante/blind configuration (optional) |
| `limitType` | LimitType | Betting limit type |
| `wildcardRules` | WildcardRulesDto? | Wildcard configuration (optional) |
| `showdownRules` | ShowdownRulesDto? | Showdown configuration (optional) |
| `hiLoRules` | HiLoRulesDto? | Hi/Lo split rules (optional) |
| `specialRules` | SpecialRuleDto[]? | Variant-specific rules (optional) |
| `description` | string? | Variant description (optional) |

### Component DTOs

#### DeckCompositionDto

| Property | Type | Description |
|----------|------|-------------|
| `deckType` | DeckType | Full52, Short36, or Custom |
| `numberOfDecks` | int | Number of decks (default: 1) |
| `excludedCards` | string[]? | Cards to exclude (e.g., ["2h", "2d"]) |
| `includedCards` | string[]? | Cards to include (for Custom decks) |

#### CardVisibilityDto

| Property | Type | Description |
|----------|------|-------------|
| `holeCardsPrivate` | bool | Whether hole cards are private (default: true) |
| `communityCardsPublic` | bool | Whether community cards are public (default: true) |
| `faceUpIndices` | int[]? | Indices of face-up cards (for stud games) |
| `faceDownIndices` | int[]? | Indices of face-down cards (for stud games) |

#### BettingRoundDto

| Property | Type | Description |
|----------|------|-------------|
| `name` | string | Round name (e.g., "Preflop", "Flop") |
| `order` | int | Round order (0-based, sequential) |
| `communityCardsDealt` | int | Community cards dealt at start of round |
| `holeCardsDealt` | int | Hole cards dealt at start of round |
| `dealtFaceUp` | bool | Whether cards are dealt face-up |
| `minBetMultiplier` | decimal | Min bet as multiplier of big blind |
| `maxRaises` | int? | Max raises allowed (null = unlimited) |

#### HoleCardRulesDto

| Property | Type | Description |
|----------|------|-------------|
| `count` | int | Number of hole cards dealt |
| `minUsedInHand` | int | Min hole cards required in final hand |
| `maxUsedInHand` | int | Max hole cards allowed in final hand |
| `allowDraw` | bool | Whether drawing/exchanging is allowed |
| `maxDrawCount` | int | Max cards that can be drawn |

#### CommunityCardRulesDto

| Property | Type | Description |
|----------|------|-------------|
| `totalCount` | int | Total community cards |
| `minUsedInHand` | int | Min community cards in final hand |
| `maxUsedInHand` | int | Max community cards in final hand |

#### AnteBlindRulesDto

| Property | Type | Description |
|----------|------|-------------|
| `hasAnte` | bool | Whether antes are required |
| `antePercentage` | decimal | Ante as percentage of big blind |
| `hasSmallBlind` | bool | Whether small blind is required |
| `hasBigBlind` | bool | Whether big blind is required |
| `allowStraddle` | bool | Whether straddles are allowed |
| `buttonAnte` | bool | Whether button ante is used |

#### WildcardRulesDto

| Property | Type | Description |
|----------|------|-------------|
| `enabled` | bool | Whether wildcards are used |
| `wildcardCards` | string[]? | Static wildcard designations |
| `dynamic` | bool | Whether wildcards change during play |
| `dynamicRule` | string? | Rule for dynamic wildcard selection |

#### ShowdownRulesDto

| Property | Type | Description |
|----------|------|-------------|
| `showOrder` | ShowdownOrder | Order of hand reveals |
| `allowMuck` | bool | Whether mucking losing hands is allowed |
| `showAllOnAllIn` | bool | Whether all hands shown on all-in |

#### HiLoRulesDto

| Property | Type | Description |
|----------|------|-------------|
| `enabled` | bool | Whether this is a hi/lo game |
| `lowQualifier` | int | Qualifier for low (8 = eight-or-better) |
| `acePlaysLow` | bool | Whether ace can be low |
| `straightsFlushesCountAgainstLow` | bool | Whether straights/flushes hurt low |

#### SpecialRuleDto

| Property | Type | Description |
|----------|------|-------------|
| `id` | string | Unique rule identifier |
| `name` | string | Rule display name |
| `description` | string | Rule description |
| `enabled` | bool | Whether rule is active |

### Enums

#### DeckType
- `Full52` - Standard 52-card deck
- `Short36` - Short deck (6-A only)
- `Custom` - Custom deck composition

#### LimitType
- `NoLimit` - No limit on bet sizes
- `FixedLimit` - Fixed bet sizes
- `PotLimit` - Maximum bet is pot size
- `SpreadLimit` - Min/max betting range

#### ShowdownOrder
- `LastAggressor` - Last aggressor shows first
- `ClockwiseFromButton` - Clockwise from dealer
- `CounterClockwiseFromButton` - Counter-clockwise
- `Simultaneous` - All show at once

### Predefined Rulesets

The library includes predefined rulesets for common variants:

#### Texas Hold'em (No Limit)
```csharp
var holdem = PredefinedRuleSets.TexasHoldem;
// or
var holdem = PredefinedRuleSets.GetByVariant(PokerVariant.TexasHoldem);
```

**Configuration:**
- 2 hole cards, use 0-2 in final hand
- 5 community cards, use 0-5 in final hand
- 4 betting rounds: Preflop, Flop (3), Turn (1), River (1)
- No Limit betting
- Small blind + Big blind structure

#### Omaha (Pot Limit)
```csharp
var omaha = PredefinedRuleSets.Omaha;
// or
var omaha = PredefinedRuleSets.GetByVariant(PokerVariant.Omaha);
```

**Configuration:**
- 4 hole cards, must use exactly 2 in final hand
- 5 community cards, must use exactly 3 in final hand
- 4 betting rounds: Preflop, Flop (3), Turn (1), River (1)
- Pot Limit betting
- Small blind + Big blind structure

### Serialization

Use `RuleSetSerializer` for JSON serialization:

```csharp
using CardGames.Poker.Shared.Serialization;

// Serialize
string json = RuleSetSerializer.Serialize(ruleSet);

// Deserialize
RuleSetDto ruleSet = RuleSetSerializer.Deserialize(json);

// Deserialize with validation
RuleSetDto validated = RuleSetSerializer.DeserializeAndValidate(json);

// Try-pattern variants
if (RuleSetSerializer.TryDeserialize(json, out var ruleSet))
{
    // Success
}

if (RuleSetSerializer.TryDeserializeAndValidate(json, out var ruleSet, out var errors))
{
    // Success
}
```

### Validation

Use `RuleSetValidator` to validate configurations:

```csharp
using CardGames.Poker.Shared.Validation;

// Get validation errors
IReadOnlyList<string> errors = RuleSetValidator.Validate(ruleSet);

// Check if valid
bool isValid = RuleSetValidator.IsValid(ruleSet);

// Validate and throw on error
RuleSetValidator.ValidateAndThrow(ruleSet); // Throws RuleSetValidationException
```

**Validation Rules:**
- Schema version must be compatible (1.x)
- Id and Name are required
- At least one betting round required
- Betting round orders must be sequential (0, 1, 2, ...)
- Hole card count must be at least 1
- Community cards dealt in rounds must match TotalCount
- Custom decks require IncludedCards
- Wildcards require WildcardCards or DynamicRule
- Hi/Lo qualifier must be 0-8

### Example JSON

```json
{
  "schemaVersion": "1.0",
  "id": "texas-holdem-nolimit",
  "name": "Texas Hold'em (No Limit)",
  "variant": "texasHoldem",
  "deckComposition": {
    "deckType": "full52",
    "numberOfDecks": 1
  },
  "cardVisibility": {
    "holeCardsPrivate": true,
    "communityCardsPublic": true
  },
  "bettingRounds": [
    { "name": "Preflop", "order": 0, "holeCardsDealt": 2 },
    { "name": "Flop", "order": 1, "communityCardsDealt": 3 },
    { "name": "Turn", "order": 2, "communityCardsDealt": 1 },
    { "name": "River", "order": 3, "communityCardsDealt": 1 }
  ],
  "holeCardRules": {
    "count": 2,
    "minUsedInHand": 0,
    "maxUsedInHand": 2
  },
  "communityCardRules": {
    "totalCount": 5,
    "minUsedInHand": 0,
    "maxUsedInHand": 5
  },
  "anteBlindRules": {
    "hasSmallBlind": true,
    "hasBigBlind": true,
    "allowStraddle": true
  },
  "limitType": "noLimit",
  "showdownRules": {
    "showOrder": "lastAggressor",
    "allowMuck": true,
    "showAllOnAllIn": true
  },
  "description": "The most popular poker variant."
}
```

---

## Dependency Graph

```
┌─────────────────────────┐
│   CardGames.AppHost     │ (Aspire Orchestration)
├─────────────────────────┤
│  References:            │
│  - Poker.Api            │
│  - Poker.Web            │
└─────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────┐
│                                                         │
│  ┌───────────────────┐       ┌───────────────────┐     │
│  │ CardGames.Poker   │       │ CardGames.Poker   │     │
│  │       .Api        │       │       .Web        │     │
│  ├───────────────────┤       ├───────────────────┤     │
│  │ References:       │       │ References:       │     │
│  │ - ServiceDefaults │       │ - ServiceDefaults │     │
│  │ - Poker.Shared    │       │ - Poker.Shared    │     │
│  │ - Poker (domain)  │       └───────────────────┘     │
│  └───────────────────┘                                 │
│          │                            │                │
│          └────────────┬───────────────┘                │
│                       ▼                                │
│          ┌───────────────────────┐                     │
│          │  CardGames.Poker      │                     │
│          │       .Shared         │                     │
│          └───────────────────────┘                     │
│                       │                                │
│                       ▼                                │
│          ┌───────────────────────┐                     │
│          │  CardGames.Service    │                     │
│          │      Defaults         │                     │
│          └───────────────────────┘                     │
│                                                        │
└────────────────────────────────────────────────────────┘
                        │
                        ▼
         ┌───────────────────────────┐
         │    CardGames.Poker        │ (Domain Library)
         ├───────────────────────────┤
         │ References:               │
         │ - CardGames.Core.French   │
         │ - CardGames.Core          │
         └───────────────────────────┘
```

---

## API Endpoint Design (Minimal APIs)

### Endpoint Registration Pattern

```csharp
// Features/Games/GamesModule.cs
public static class GamesModule
{
    public static IEndpointRouteBuilder MapGamesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games")
            .WithTags("Games")
            .WithOpenApi();

        group.MapPost("/", CreateGameEndpoint.Handle)
            .WithName("CreateGame");
        
        group.MapGet("/", ListGamesEndpoint.Handle)
            .WithName("ListGames");
        
        group.MapGet("/{id:guid}", GetGameEndpoint.Handle)
            .WithName("GetGame");

        return app;
    }
}
```

### Example Vertical Slice (Create Game Feature)

```csharp
// CreateGameCommand.cs
public record CreateGameCommand(
    string Name,
    PokerVariant Variant,
    int MaxPlayers
) : IRequest<GameResponse>;

// CreateGameHandler.cs
public class CreateGameHandler : IRequestHandler<CreateGameCommand, GameResponse>
{
    private readonly AppDbContext _context;

    public CreateGameHandler(AppDbContext context) => _context = context;

    public async Task<GameResponse> Handle(
        CreateGameCommand request, 
        CancellationToken cancellationToken)
    {
        var game = new Game
        {
            Name = request.Name,
            Variant = request.Variant,
            MaxPlayers = request.MaxPlayers,
            State = GameState.Waiting
        };

        _context.Games.Add(game);
        await _context.SaveChangesAsync(cancellationToken);

        return game.ToResponse();
    }
}

// CreateGameValidator.cs
public class CreateGameValidator : AbstractValidator<CreateGameCommand>
{
    public CreateGameValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.MaxPlayers)
            .InclusiveBetween(2, 10);
    }
}

// CreateGameEndpoint.cs
public static class CreateGameEndpoint
{
    public static async Task<IResult> Handle(
        CreateGameCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Results.Created($"/api/games/{result.Id}", result);
    }
}
```

---

## Database Configuration

### SQL Server Connection (via Aspire)

The database connection is managed through .NET Aspire's service defaults:

```csharp
// AppHost.cs
var sql = builder.AddSqlServer("sql")
    .AddDatabase("cardgames");

var api = builder.AddProject<Projects.CardGames_Poker_Api>("api")
    .WithReference(sql);
```

### Entity Framework Core Configuration

```csharp
// Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) 
        : base(options) { }

    public DbSet<Game> Games => Set<Game>();
    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
```

---

## Getting Started

### Prerequisites

- .NET 10 SDK
- SQL Server (LocalDB or container)
- Docker (for Aspire orchestration)

### Running the Application

```bash
# From the src directory
cd src

# Run with Aspire
dotnet run --project CardGames.AppHost

# Or run individual projects
dotnet run --project CardGames.Poker.Api
dotnet run --project CardGames.Poker.Web
```

### Creating Database Migrations

```bash
# From the API project directory
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## Future Considerations

1. **SignalR Integration**: Real-time game updates
2. **Authentication/Authorization**: Identity Server or Azure AD B2C
3. **Caching**: Redis for session state and game caching
4. **Message Queue**: Azure Service Bus or RabbitMQ for event-driven features
5. **API Versioning**: Support multiple API versions
