# Integration Test Suite

This document describes the end-to-end integration tests for the CardGames application, covering complete gameplay flows for all supported poker variants.

## Table of Contents

- [Test Coverage Overview](#test-coverage-overview)
- [Test Categories](#test-categories)
- [Gameplay Flow Tests](#gameplay-flow-tests)
- [SignalR Integration Tests](#signalr-integration-tests)
- [Running Integration Tests](#running-integration-tests)
- [Test Utilities](#test-utilities)
- [Writing New Tests](#writing-new-tests)

---

## Test Coverage Overview

The integration test suite covers the following areas:

| Category | Test Project | Coverage |
|----------|--------------|----------|
| API Endpoints | `CardGames.Poker.Api.Tests` | 335 tests |
| Web Components | `CardGames.Poker.Web.Tests` | 121 tests |
| Domain Logic | `CardGames.Poker.Tests` | 829 tests |
| CLI | `CardGames.Poker.CLI.Tests` | 51 tests |
| Shared Contracts | `CardGames.Poker.Shared.Tests` | 172 tests |

---

## Test Categories

### 1. Variant-Specific End-to-End Tests

Complete gameplay flow tests for each poker variant:

| Test Class | Variant | Key Flows |
|------------|---------|-----------|
| `HoldEmApiEndToEndTests` | Texas Hold'em | Create table, join, bet, showdown |
| `OmahaApiEndToEndTests` | Omaha | 4-hole card rules, PLO betting |
| `SevenCardStudApiEndToEndTests` | 7 Card Stud | Ante, bring-in, street progression |
| `FiveCardDrawApiEndToEndTests` | 5 Card Draw | Draw phase, wild cards |
| `FollowTheQueenApiEndToEndTests` | Follow the Queen | Wild card rules |

### 2. Feature-Specific Tests

| Test Class | Feature | Coverage |
|------------|---------|----------|
| `GameHubTests` | SignalR Hub | Connection, actions, events |
| `ConnectionMappingServiceTests` | Connection tracking | Join, leave, reconnect |
| `ChatServiceTests` | Chat system | Messages, muting, moderation |
| `ShowdownCoordinatorTests` | Showdown logic | Card reveals, winners |
| `TablesEndpointTests` | Table management | CRUD, filtering, quick join |
| `SeatingEndpointTests` | Seat management | Seat selection, waiting list |

### 3. Contract Tests

| Test Class | Purpose |
|------------|---------|
| `VariantsEndpointTests` | Variant API contracts |
| `SimulationsEndpointTests` | Simulation API contracts |
| `HandsEndpointTests` | Hand evaluation API |

---

## Gameplay Flow Tests

### Texas Hold'em Complete Flow

Located in `HoldEmApiEndToEndTests.cs`:

```csharp
[Fact]
public async Task HoldEmFlow_CreateTable_JoinTable_LeaveTable()
{
    // Step 1: Create a Hold'em table
    var createRequest = new CreateTableRequest(
        Name: "E2E Flow Test Table",
        Variant: PokerVariant.TexasHoldem,
        SmallBlind: 1,
        BigBlind: 2,
        MinBuyIn: 40,
        MaxBuyIn: 200,
        MaxSeats: 6,
        Privacy: TablePrivacy.Public);

    var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
    createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    
    // Step 2: Join the table
    var joinResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", joinRequest);
    joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    
    // Step 3: Verify seat assignment
    var tablesResponse = await _client.GetAsync("/api/tables");
    table.OccupiedSeats.Should().Be(1);
    
    // Step 4: Leave the table
    var leaveResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/leave", leaveRequest);
    leaveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### Waiting List Flow

```csharp
[Fact]
public async Task HoldEmFlow_CreateTable_FillTable_JoinWaitingList()
{
    // Step 1: Create small table
    var createRequest = new CreateTableRequest(MaxSeats: 2, ...);
    
    // Step 2: Fill the table
    await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", ...);
    await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", ...);
    
    // Step 3: Table should be full
    table.OccupiedSeats.Should().Be(2);
    table.MaxSeats.Should().Be(2);
    
    // Step 4: Join waiting list
    var waitingListResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", ...);
    waitingListResult.Entry.Position.Should().Be(1);
    
    // Step 5: Verify waiting list
    var getWaitingListResponse = await _client.GetAsync($"/api/tables/{tableId}/waiting-list");
    getWaitingListResult.Entries.Should().HaveCount(1);
}
```

### RuleSet Validation

```csharp
[Fact]
public void PredefinedRuleSet_TexasHoldem_HasCorrectConfiguration()
{
    var ruleSet = PredefinedRuleSets.TexasHoldem;
    
    // Deck composition
    ruleSet.DeckComposition.DeckType.Should().Be(DeckType.Full52);
    
    // Hole card rules
    ruleSet.HoleCardRules.Count.Should().Be(2);
    ruleSet.HoleCardRules.MinUsedInHand.Should().Be(0);
    ruleSet.HoleCardRules.MaxUsedInHand.Should().Be(2);
    
    // Community card rules
    ruleSet.CommunityCardRules.TotalCount.Should().Be(5);
    
    // Betting rounds
    ruleSet.BettingRounds.Should().HaveCount(4);
    ruleSet.BettingRounds[0].Name.Should().Be("Preflop");
    ruleSet.BettingRounds[1].Name.Should().Be("Flop");
    ruleSet.BettingRounds[2].Name.Should().Be("Turn");
    ruleSet.BettingRounds[3].Name.Should().Be("River");
}
```

---

## SignalR Integration Tests

### GameHub Tests

Located in `GameHubTests.cs`:

```csharp
public class GameHubTests
{
    private readonly GameHub _hub;
    private readonly IConnectionMappingService _connectionMapping;
    private readonly IHubCallerClients _mockClients;
    
    [Fact]
    public async Task JoinTable_NewConnection_AddsToMappingAndGroup()
    {
        // Arrange
        _connectionMapping.TryReconnect(...)
            .Returns((PlayerConnectionInfo?)null);

        // Act
        await _hub.JoinTable(tableId, playerName);

        // Assert
        _connectionMapping.Received(1).AddConnection(TestConnectionId, playerName, tableId);
        await _mockGroups.Received(1).AddToGroupAsync(TestConnectionId, tableId, default);
        await _mockGroup.Received(1).SendCoreAsync("PlayerConnected", Arg.Any<object[]>(), default);
    }
    
    [Fact]
    public async Task JoinTable_Reconnection_DoesNotAddNewMapping()
    {
        // Arrange - existing connection info
        var oldInfo = new PlayerConnectionInfo(...);
        _connectionMapping.TryReconnect(TestConnectionId, playerName, tableId).Returns(oldInfo);

        // Act
        await _hub.JoinTable(tableId, playerName);

        // Assert
        _connectionMapping.DidNotReceive().AddConnection(...);
        await _mockGroup.Received(1).SendCoreAsync("PlayerReconnected", ...);
    }
}
```

### Betting Action Tests

```csharp
[Fact]
public async Task Fold_WithValidConnection_BroadcastsActionToGroup()
{
    // Arrange
    _connectionMapping.GetPlayerInfo(TestConnectionId).Returns(playerInfo);

    // Act
    await _hub.Fold(tableId);

    // Assert
    _connectionMapping.Received(1).UpdateLastActivity(TestConnectionId);
    await _mockGroup.Received(1).SendCoreAsync("PlayerAction", Arg.Any<object[]>(), default);
}

[Fact]
public async Task PlayerAction_WithInvalidConnection_RejectsAction()
{
    // Arrange
    _connectionMapping.GetPlayerInfo(TestConnectionId).Returns((PlayerConnectionInfo?)null);

    // Act
    await _hub.Fold(tableId);

    // Assert
    await _mockCaller.Received(1).SendCoreAsync("ActionRejected", ...);
    await _mockGroup.DidNotReceive().SendCoreAsync("PlayerAction", ...);
}
```

---

## Running Integration Tests

### All Tests

```bash
cd src
dotnet test
```

### Specific Test Project

```bash
dotnet test Tests/CardGames.Poker.Api.Tests/
```

### Specific Test Class

```bash
dotnet test --filter FullyQualifiedName~HoldEmApiEndToEndTests
```

### Specific Test Method

```bash
dotnet test --filter "FullyQualifiedName~HoldEmFlow_CreateTable"
```

### With Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Verbose Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

---

## Test Utilities

### WebApplicationFactory

API tests use `WebApplicationFactory<Program>` for in-memory hosting:

```csharp
public class HoldEmApiEndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HoldEmApiEndToEndTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
}
```

### Mocking with NSubstitute

```csharp
// Create mocks
_logger = Substitute.For<ILogger<GameHub>>();
_connectionMapping = Substitute.For<IConnectionMappingService>();
_mockClients = Substitute.For<IHubCallerClients>();

// Setup returns
_connectionMapping.GetPlayerInfo(connectionId).Returns(playerInfo);
_mockClients.Group(tableId).Returns(_mockGroup);

// Verify calls
_connectionMapping.Received(1).AddConnection(...);
await _mockGroup.Received(1).SendCoreAsync("EventName", Arg.Any<object[]>(), default);
```

### Setting Hub Context

For testing SignalR hubs:

```csharp
// Use reflection to set Hub properties
var clientsProperty = typeof(Hub).GetProperty("Clients");
clientsProperty!.SetValue(_hub, _mockClients);

var contextProperty = typeof(Hub).GetProperty("Context");
contextProperty!.SetValue(_hub, _mockContext);

var groupsProperty = typeof(Hub).GetProperty("Groups");
groupsProperty!.SetValue(_hub, _mockGroups);
```

---

## Writing New Tests

### Test Naming Convention

```
{Method}_{Scenario}_{ExpectedResult}
```

Examples:
- `CreateTable_WithValidRequest_ReturnsCreated`
- `JoinTable_WhenTableFull_ReturnsTableFullError`
- `Fold_WithInvalidConnection_RejectsAction`

### Test Structure (AAA Pattern)

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var request = new CreateTableRequest(...);
    _mockService.GetSomething().Returns(expectedValue);
    
    // Act
    var response = await _client.PostAsJsonAsync("/api/endpoint", request);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    var result = await response.Content.ReadFromJsonAsync<ResponseType>();
    result.Should().NotBeNull();
    result!.SomeProperty.Should().Be(expectedValue);
}
```

### FluentAssertions Examples

```csharp
// Basic assertions
result.Should().NotBeNull();
result.Success.Should().BeTrue();
result.Table.Should().NotBeNull();

// Collection assertions
result.Tables.Should().HaveCount(3);
result.Tables.Should().OnlyContain(t => t.Variant == PokerVariant.TexasHoldem);
result.Entries.Should().ContainSingle();

// Exception assertions
var act = async () => await _client.GetAsync("/invalid");
await act.Should().ThrowAsync<HttpRequestException>();

// Object comparison
result.Config.Should().BeEquivalentTo(expectedConfig);
```

### Adding a New E2E Test

1. **Create test class** for the feature:
   ```csharp
   public class NewFeatureEndToEndTests : IClassFixture<WebApplicationFactory<Program>>
   {
       private readonly HttpClient _client;
       
       public NewFeatureEndToEndTests(WebApplicationFactory<Program> factory)
       {
           _client = factory.CreateClient();
       }
   }
   ```

2. **Add test methods** for each scenario:
   ```csharp
   [Fact]
   public async Task NewFeature_BasicFlow_Succeeds()
   {
       // Test implementation
   }
   ```

3. **Run the test** to verify:
   ```bash
   dotnet test --filter FullyQualifiedName~NewFeatureEndToEndTests
   ```

---

## Test Data Management

### Predefined Test Data

```csharp
private static readonly string TestTableId = Guid.NewGuid().ToString();
private static readonly string TestConnectionId = "test-connection-id";

private PlayerConnectionInfo CreateTestPlayerInfo(string name = "TestPlayer") =>
    new PlayerConnectionInfo(
        TestConnectionId,
        name,
        TestTableId,
        DateTime.UtcNow,
        DateTime.UtcNow);
```

### Shared Test Fixtures

For tests that need shared setup:

```csharp
public class SharedTestFixture : IAsyncLifetime
{
    public HttpClient Client { get; private set; }
    
    public async Task InitializeAsync()
    {
        // Setup code
    }
    
    public async Task DisposeAsync()
    {
        // Cleanup code
    }
}

public class MyTests : IClassFixture<SharedTestFixture>
{
    private readonly SharedTestFixture _fixture;
    
    public MyTests(SharedTestFixture fixture)
    {
        _fixture = fixture;
    }
}
```

---

## Continuous Integration

Tests run automatically on every pull request via GitHub Actions:

```yaml
# .github/workflows/CI.yml
- name: Test
  run: dotnet test --no-build --verbosity normal
```

### Test Results

- Test results are displayed in the PR checks
- Coverage reports can be generated with additional configuration
- Failed tests block PR merging
