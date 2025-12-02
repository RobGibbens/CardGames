# Developer Setup Guide

This guide provides instructions for setting up a local development environment for the CardGames project.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Project Structure](#project-structure)
- [Building the Solution](#building-the-solution)
- [Running the Application](#running-the-application)
- [Running Tests](#running-tests)
- [Development Workflow](#development-workflow)
- [Debugging](#debugging)
- [Code Style and Conventions](#code-style-and-conventions)
- [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Software

| Software | Minimum Version | Purpose |
|----------|-----------------|---------|
| .NET SDK | 10.0 | Runtime and build tools |
| Docker | 24.0+ | Container runtime for Aspire |
| Visual Studio 2022 | 17.8+ | IDE (optional, VS Code works too) |
| VS Code | Latest | Alternative IDE |
| Git | 2.40+ | Version control |

### Recommended Extensions (VS Code)

- C# Dev Kit (`ms-dotnettools.csdevkit`)
- .NET Aspire (`ms-dotnettools.dotnet-aspire`)
- EditorConfig (`editorconfig.editorconfig`)

### Recommended Extensions (Visual Studio 2022)

- .NET Aspire workload
- Web Development workload

---

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/EluciusFTW/CardGames.git
cd CardGames
```

### 2. Restore Dependencies

```bash
cd src
dotnet restore
```

### 3. Verify Setup

```bash
dotnet build
dotnet test
```

If all tests pass, your environment is ready!

---

## Project Structure

```
CardGames/
├── src/
│   ├── CardGames.Core/              # Core abstractions (.NET Standard 2.1)
│   ├── CardGames.Core.French/       # French-suited cards (.NET Standard 2.1)
│   ├── CardGames.Poker/             # Poker domain library (.NET 10)
│   ├── CardGames.Poker.Api/         # Backend REST API + SignalR
│   ├── CardGames.Poker.Web/         # Blazor Server Frontend
│   ├── CardGames.Poker.Shared/      # Shared DTOs and contracts
│   ├── CardGames.Poker.CLI/         # Console interface
│   ├── CardGames.AppHost/           # .NET Aspire orchestration
│   ├── CardGames.ServiceDefaults/   # Shared service configuration
│   ├── CardGames.Core.Benchmarks/   # Core performance benchmarks
│   └── Tests/
│       ├── CardGames.Core.Tests/
│       ├── CardGames.Core.French.Tests/
│       ├── CardGames.Poker.Tests/
│       ├── CardGames.Poker.Api.Tests/
│       ├── CardGames.Poker.Web.Tests/
│       ├── CardGames.Poker.CLI.Tests/
│       └── CardGames.Poker.Shared.Tests/
├── architecture/                    # Architecture documentation
├── docs/                            # User and API documentation
└── sample/                          # Sample screenshots
```

---

## Building the Solution

### Full Build

```bash
cd src
dotnet build
```

### Release Build

```bash
dotnet build -c Release
```

### Build Specific Project

```bash
dotnet build CardGames.Poker.Api/CardGames.Poker.Api.csproj
```

---

## Running the Application

### Using .NET Aspire (Recommended)

The Aspire orchestrator manages all services:

```bash
cd src/CardGames.AppHost
dotnet run
```

This starts:
- API server (`CardGames.Poker.Api`)
- Web frontend (`CardGames.Poker.Web`)
- Any configured infrastructure (SQL Server, Redis, etc.)

Access the Aspire dashboard at the URL shown in the console output.

### Running Individual Projects

**API Server:**
```bash
cd src/CardGames.Poker.Api
dotnet run
```
Default URL: `https://localhost:7001`

**Web Frontend:**
```bash
cd src/CardGames.Poker.Web
dotnet run
```
Default URL: `https://localhost:7002`

**CLI Application:**
```bash
cd src/CardGames.Poker.CLI
dotnet run
```

### Environment Configuration

Configuration files by environment:
- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides
- `appsettings.Staging.json` - Staging overrides

---

## Running Tests

### All Tests

```bash
cd src
dotnet test
```

### Specific Test Project

```bash
dotnet test Tests/CardGames.Poker.Api.Tests/
```

### With Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Or using dotnet-coverage:
```bash
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

### Filtering Tests

```bash
# Run specific test class
dotnet test --filter FullyQualifiedName~GameHubTests

# Run tests matching a pattern
dotnet test --filter "Category=Integration"
```

### Watch Mode (for TDD)

```bash
dotnet watch test --project Tests/CardGames.Poker.Tests/
```

---

## Development Workflow

### Making Changes

1. Create a feature branch:
   ```bash
   git checkout -b feature/my-feature
   ```

2. Make your changes

3. Run tests locally:
   ```bash
   dotnet test
   ```

4. Commit and push:
   ```bash
   git add .
   git commit -m "feat: add my feature"
   git push origin feature/my-feature
   ```

5. Open a pull request

### Hot Reload

Enable hot reload for rapid development:

```bash
dotnet watch run --project CardGames.Poker.Web/
```

Changes to `.razor` files and C# code will automatically reload.

### Running Benchmarks

Performance benchmarks use BenchmarkDotNet:

```bash
cd src/CardGames.Core.Benchmarks
dotnet run -c Release
```

**Note:** Always run benchmarks in Release configuration for accurate results.

---

## Debugging

### Visual Studio

1. Open `CardGames.sln`
2. Set `CardGames.AppHost` as startup project
3. Press F5 to start debugging

### VS Code

1. Open the `src` folder
2. Use the provided launch configurations in `.vscode/launch.json`
3. Press F5 to start debugging

### Debugging SignalR

Enable SignalR logging in `Program.cs`:

```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});
```

Client-side logging:
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/gamehub")
    .configureLogging(signalR.LogLevel.Debug)
    .build();
```

### Viewing Logs

Application logs are output to the console by default. Configure logging in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.SignalR": "Debug"
    }
  }
}
```

---

## Code Style and Conventions

### General Guidelines

- Use file-scoped namespaces
- Prefer modern C# features (records, pattern matching)
- Follow the domain terminology (Dealer, Deck, Hand, etc.)
- Return `IReadOnlyCollection<T>` instead of `IEnumerable<T>`

### Naming Conventions

- **Types:** PascalCase (`HandEvaluator`, `CardDto`)
- **Methods:** PascalCase (`DealCard`, `EvaluateHand`)
- **Variables:** camelCase (`playerName`, `currentBet`)
- **Constants:** PascalCase (`MaxPlayers`, `DefaultTimeout`)
- **Private fields:** _camelCase (`_logger`, `_connectionMapping`)

### Testing Conventions

- Test class: `{ClassName}Tests`
- Test method: `{Method}_{Scenario}` or `{Behavior}_When_{Condition}`
- Use xUnit, FluentAssertions, and NSubstitute
- Follow Arrange-Act-Assert pattern

Example:
```csharp
[Fact]
public void Fold_WithValidConnection_BroadcastsActionToGroup()
{
    // Arrange
    _connectionMapping.GetPlayerInfo(TestConnectionId).Returns(playerInfo);

    // Act
    await _hub.Fold(tableId);

    // Assert
    await _mockGroup.Received(1).SendCoreAsync("PlayerAction", Arg.Any<object[]>(), default);
}
```

### EditorConfig

The project includes an `.editorconfig` file with style rules. Ensure your IDE is configured to use it.

---

## Troubleshooting

### Build Errors

**"SDK not found"**
```bash
# Check installed SDKs
dotnet --list-sdks

# Install the required SDK from:
# https://dotnet.microsoft.com/download
```

**"Package restore failed"**
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore again
dotnet restore
```

### Runtime Errors

**"Connection refused" when running Aspire**
- Ensure Docker is running
- Check that required ports are not in use

**SignalR connection fails**
- Check CORS configuration in `Program.cs`
- Verify the hub URL matches your client configuration
- Check browser console for errors

### Test Failures

**Flaky tests**
- Some integration tests may be timing-sensitive
- Run tests individually to isolate issues
- Check for race conditions in async code

**"Assembly not found"**
```bash
# Rebuild the solution
dotnet build

# Run tests again
dotnet test
```

### Docker Issues

**"Cannot connect to Docker daemon"**
- Start Docker Desktop
- On Linux, ensure the Docker service is running:
  ```bash
  sudo systemctl start docker
  ```

**Container fails to start**
```bash
# Check container logs
docker logs <container-id>

# Remove and recreate containers
docker compose down -v
docker compose up -d
```

---

## Additional Resources

- [.NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [ASP.NET Core SignalR](https://docs.microsoft.com/en-us/aspnet/core/signalr/)
- [Blazor Documentation](https://docs.microsoft.com/en-us/aspnet/core/blazor/)
- [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [xUnit Documentation](https://xunit.net/docs/getting-started/netcore/cmdline)
- [FluentAssertions](https://fluentassertions.com/)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)

---

## Getting Help

If you encounter issues:

1. Check the [Troubleshooting](#troubleshooting) section above
2. Search existing GitHub issues
3. Open a new issue with:
   - .NET SDK version (`dotnet --version`)
   - Operating system
   - Steps to reproduce
   - Error messages and logs
