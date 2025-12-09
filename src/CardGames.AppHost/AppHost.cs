using CardGames.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var serviceBus = builder.AddAndConfigureServiceBus();

var cache = builder
	.AddRedis("cache")
	.WithLifetime(ContainerLifetime.Persistent);

var sqlServer = builder.AddAzureSqlServer("sqlserver")
	.RunAsContainer(c =>
	{
		c.WithLifetime(ContainerLifetime.Persistent);
	});

var sqldb = sqlServer.AddDatabase("cardsdb");

var migrations = builder.AddProject<Projects.CardGames_MigrationService>("migrations")
	.WithReference(sqldb)
	.WaitFor(sqldb);

var api = builder.AddProject<Projects.CardGames_Poker_Api>("api")
.WithReference(serviceBus)
.WaitFor(serviceBus)
.WithReference(cache)
.WaitFor(cache)
.WithReference(sqldb)
.WaitFor(sqldb)
.WithReference(migrations)
.WaitForCompletion(migrations);

var web = builder.AddProject<Projects.CardGames_Poker_Web>("web")
	.WithReference(api)
	.WaitFor(api);

//var cli = builder.AddProject<Projects.CardGames_Poker_CLI>("cli")
//	.WithReference(api)
//	.WaitFor(api);

builder.Build().Run();
