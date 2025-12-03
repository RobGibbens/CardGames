var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.CardGames_Poker_Api>("api");

builder.AddProject<Projects.CardGames_Poker_Web>("web")
	.WithReference(api)
	.WaitFor(api);

builder.Build().Run();
