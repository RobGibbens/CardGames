var builder = DistributedApplication.CreateBuilder(args);

// Configure the API service with Aspire orchestration
var api = builder.AddProject<Projects.CardGames_Poker_Api>("cardgames-poker-api");

// Configure the Web service with a reference to the API for service discovery
builder.AddProject<Projects.CardGames_Poker_Web>("cardgames-poker-web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
