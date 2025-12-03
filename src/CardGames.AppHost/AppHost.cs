var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
	.WithPgAdmin()
	.WithDataVolume();

var martenDb = postgres.AddDatabase("marten");

var api = builder.AddProject<Projects.CardGames_Poker_Api>("api")
	.WithReference(martenDb);

var web = builder.AddProject<Projects.CardGames_Poker_Web>("web")
	.WithReference(api)
	.WaitFor(api);

var cli = builder.AddProject<Projects.CardGames_Poker_CLI>("cli")
	.WithReference(api)
	.WaitFor(api);

builder.Build().Run();
