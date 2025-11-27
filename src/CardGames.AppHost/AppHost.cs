var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CardGames_Poker_Web>("cardgames-poker-web");

builder.AddProject<Projects.CardGames_Poker_Api>("cardgames-poker-api");

builder.Build().Run();
