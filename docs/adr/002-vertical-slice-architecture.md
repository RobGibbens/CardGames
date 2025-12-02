# ADR-002: Vertical Slice Architecture

## Status

Accepted

## Context

The CardGames application needs an architectural pattern that:

1. Organizes code by business feature rather than technical layer
2. Minimizes coupling between different features
3. Allows features to evolve independently
4. Supports the CQRS pattern for separating reads and writes
5. Makes it easy to understand and test individual features
6. Scales well as we add more poker variants and game features

Traditional layered architecture (Controllers → Services → Repositories) creates:
- High coupling between layers
- Changes to one feature often ripple through multiple layers
- Difficulty understanding a complete feature
- Tendency toward "anemic" service layers

## Decision

We will use **Vertical Slice Architecture** for both the API and Web projects.

Each feature is organized as a self-contained "slice" containing:
- **Request/Command/Query** - Input DTO
- **Handler** - Business logic
- **Validator** - Input validation (FluentValidation)
- **Endpoint** - HTTP route (Minimal API)
- **Response** - Output DTO (if different from shared contracts)

### Folder Structure

```
Features/
├── Tables/
│   ├── CreateTable/
│   │   ├── CreateTableCommand.cs
│   │   ├── CreateTableHandler.cs
│   │   ├── CreateTableValidator.cs
│   │   └── CreateTableEndpoint.cs
│   ├── GetTable/
│   │   ├── GetTableQuery.cs
│   │   ├── GetTableHandler.cs
│   │   └── GetTableEndpoint.cs
│   └── TablesModule.cs
├── Showdown/
│   ├── ShowdownCoordinator.cs
│   └── ShowdownModule.cs
└── ...
```

### CQRS Integration

- **Commands** for write operations: `CreateTableCommand`, `JoinTableCommand`
- **Queries** for read operations: `GetTableQuery`, `ListTablesQuery`
- MediatR dispatches commands/queries to handlers
- Clear separation of read vs write code paths

## Consequences

### Positive

- **Feature isolation** - Each feature is self-contained and easy to understand
- **Reduced coupling** - Features don't depend on each other directly
- **Easier testing** - Test one slice without mocking many layers
- **CQRS-friendly** - Natural separation of commands and queries
- **Scalability** - Easy to add new features without affecting existing ones
- **Parallel development** - Teams can work on different features independently
- **Discoverability** - Find all code for a feature in one place

### Negative

- **Some duplication** - Similar patterns repeated across slices
- **Cross-cutting concerns** - Need MediatR pipeline behaviors for shared logic
- **Learning curve** - Different from traditional layered architecture
- **Temptation to share** - Resist urge to create shared "services"

### Neutral

- **MediatR dependency** - Required for dispatch, but well-established library
- **More files** - More files but better organization

## Implementation Guidelines

### 1. Keep Slices Independent

```csharp
// Good - slice handles its own logic
public class CreateTableHandler : IRequestHandler<CreateTableCommand, TableResponse>
{
    public async Task<TableResponse> Handle(CreateTableCommand request, ...)
    {
        // All logic within the handler
    }
}

// Avoid - depending on other feature's handler
public class JoinTableHandler : IRequestHandler<JoinTableCommand, ...>
{
    private readonly CreateTableHandler _createHandler; // Don't do this
}
```

### 2. Share DTOs, Not Logic

```csharp
// Shared contracts in CardGames.Poker.Shared
public record TableDto(...);

// Each handler uses shared DTOs but implements its own logic
```

### 3. Use Pipeline Behaviors for Cross-Cutting Concerns

```csharp
// Validation behavior applies to all commands
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    // Runs FluentValidation before handler
}

// Logging behavior for all requests
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    // Logs request/response
}
```

### 4. Module Registration

```csharp
// Each feature has a module for endpoint registration
public static class TablesModule
{
    public static IEndpointRouteBuilder MapTablesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tables").WithTags("Tables");
        
        group.MapPost("/", CreateTableEndpoint.Handle);
        group.MapGet("/{id:guid}", GetTableEndpoint.Handle);
        
        return app;
    }
}
```

## Alternatives Considered

### 1. Traditional Layered Architecture

**Rejected because:**
- High coupling between layers
- Hard to understand complete features
- Changes ripple through multiple layers
- Leads to anemic domain model

### 2. Clean Architecture (Onion)

**Rejected because:**
- More complex for our use case
- Adds abstraction layers we don't need
- Harder to navigate codebase
- Over-engineered for feature-based development

### 3. Modular Monolith

**Partially adopted:**
- We use modules for grouping related features
- But within modules, we follow vertical slices
- Best of both approaches

## References

- [Vertical Slice Architecture - Jimmy Bogard](https://jimmybogard.com/vertical-slice-architecture/)
- [MediatR Wiki](https://github.com/jbogard/MediatR/wiki)
- [CQRS Pattern - Martin Fowler](https://martinfowler.com/bliki/CQRS.html)
