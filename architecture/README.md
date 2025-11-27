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
│   └── GameDto.cs
├── Enums/
│   ├── GameState.cs
│   ├── HandType.cs
│   └── PokerVariant.cs
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
