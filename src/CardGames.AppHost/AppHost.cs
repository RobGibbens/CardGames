var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CardGames_Poker_Api>("api");

builder.AddProject<Projects.CardGames_Poker_Web>("web");

builder.Build().Run();
