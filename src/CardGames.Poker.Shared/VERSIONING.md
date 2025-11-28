# CardGames.Poker.Shared Versioning Guide

This document describes the versioning strategy for the `CardGames.Poker.Shared` library, which contains shared contracts, DTOs, and events used across the CardGames Poker solution.

## Overview

The Shared library is referenced by:
- **CardGames.Poker.Api** - Backend REST API
- **CardGames.Poker.Web** - Blazor Server Frontend
- **Future clients** - Mobile apps, third-party integrations, etc.

Maintaining backward compatibility is crucial since changes to this library affect multiple consumers.

## Semantic Versioning

We follow [Semantic Versioning 2.0.0](https://semver.org/) (SemVer):

```
MAJOR.MINOR.PATCH
```

- **MAJOR**: Incompatible API changes (breaking changes)
- **MINOR**: Backward-compatible functionality additions
- **PATCH**: Backward-compatible bug fixes

### Version Examples

| Version | Change Type |
|---------|-------------|
| 1.0.0 → 2.0.0 | Breaking change (removed property, renamed type) |
| 1.0.0 → 1.1.0 | New DTO, enum value, or event type added |
| 1.0.0 → 1.0.1 | XML documentation fix, typo correction |

## Breaking vs Non-Breaking Changes

### Breaking Changes (Require MAJOR version bump)

- Removing a public type, property, or method
- Renaming a public type, property, or method
- Changing the type of a property
- Removing an enum value
- Changing the order of constructor parameters in records
- Changing nullability of existing properties

### Non-Breaking Changes (Require MINOR version bump)

- Adding new DTOs, records, or classes
- Adding new enum values (at the end)
- Adding new optional properties with default values
- Adding new event types

### Patch Changes (Require PATCH version bump)

- Documentation updates
- Internal implementation changes with no public API impact

## Best Practices for Shared Contracts

### 1. Use Records for DTOs

Records provide immutability and value-based equality, making them ideal for DTOs:

```csharp
// Good - immutable record
public record PlayerDto(
    string Name,
    int ChipStack,
    bool HasFolded = false);

// Avoid - mutable class
public class PlayerDto
{
    public string Name { get; set; }
    public int ChipStack { get; set; }
}
```

### 2. Add Optional Properties with Defaults

When adding new properties, always provide default values to maintain backward compatibility:

```csharp
// Version 1.0.0
public record PlayerDto(string Name, int ChipStack);

// Version 1.1.0 - Non-breaking addition
public record PlayerDto(
    string Name, 
    int ChipStack,
    string? AvatarUrl = null);  // Optional with default
```

### 3. Use Enum Values Carefully

Add new enum values at the end to avoid serialization issues:

```csharp
// Version 1.0.0
public enum GameState
{
    WaitingForPlayers,  // 0
    InProgress,         // 1
    Ended               // 2
}

// Version 1.1.0 - Add at end
public enum GameState
{
    WaitingForPlayers,  // 0
    InProgress,         // 1
    Ended,              // 2
    Paused              // 3 - New value added at end
}
```

### 4. Never Remove or Rename Public Members

Instead of removing, mark as obsolete first:

```csharp
// Version 1.x - Mark as obsolete
[Obsolete("Use PlayerName instead. Will be removed in version 2.0.")]
public string Name => PlayerName;
public string PlayerName { get; }

// Version 2.0 - Remove in major version
public string PlayerName { get; }
```

### 5. Version Events Carefully

For event types, consider using event versioning:

```csharp
// Original event
public record PlayerJoinedEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    int ChipStack) : GameEvent(GameId, Timestamp);

// When adding significant changes, create a new version
public record PlayerJoinedEventV2(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    int ChipStack,
    string? SeatPosition) : GameEvent(GameId, Timestamp);
```

## Release Process

1. **Update Version Number**: Update the version in `CardGames.Poker.Shared.csproj`:

```xml
<PropertyGroup>
    <Version>1.2.0</Version>
</PropertyGroup>
```

2. **Update Changelog**: Document changes in CHANGELOG.md (if maintained)

3. **Review Consumers**: Ensure API and Web projects are compatible

4. **Test Integration**: Run all integration tests

5. **Tag Release**: Create a git tag for the version

## Package Reference

When referencing this library from other projects in the solution:

```xml
<ItemGroup>
  <ProjectReference Include="..\CardGames.Poker.Shared\CardGames.Poker.Shared.csproj" />
</ItemGroup>
```

For external consumers (future NuGet package):

```xml
<ItemGroup>
  <PackageReference Include="CardGames.Poker.Shared" Version="1.0.0" />
</ItemGroup>
```

## API Stability Guarantees

| Type | Stability |
|------|-----------|
| DTOs in `DTOs/` | Stable - versioned |
| Enums in `Enums/` | Stable - versioned |
| Contracts in `Contracts/` | Stable - versioned |
| Events in `Events/` | Stable - versioned |

## Migration Guide Template

When releasing a MAJOR version, provide a migration guide:

```markdown
# Migration Guide: v1.x to v2.0

## Breaking Changes

1. **PlayerDto.Name renamed to PlayerDto.PlayerName**
   - Before: `player.Name`
   - After: `player.PlayerName`

2. **GameState.Waiting removed**
   - Before: `GameState.Waiting`
   - After: `GameState.WaitingForPlayers`

## Upgrade Steps

1. Update package reference to version 2.0.0
2. Replace all uses of `Name` with `PlayerName`
3. Replace `GameState.Waiting` with `GameState.WaitingForPlayers`
4. Rebuild and test
```

## Questions?

For questions about versioning or compatibility, please open an issue in the repository.
