using CardGames.AppHost;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var serviceBus = builder.AddAndConfigureServiceBus();

var cache = builder
	.AddRedis("cache")
	.WithLifetime(ContainerLifetime.Persistent);

var blobs = builder
	.AddAzureStorage("storage")
	.RunAsEmulator(azurite =>
	{
		azurite.WithLifetime(ContainerLifetime.Persistent)
			.WithDataVolume();
	})
	.AddBlobs("blobs");

var sqlServer = builder.AddAzureSqlServer("sqlserver")
	.RunAsContainer(c => { c.WithLifetime(ContainerLifetime.Persistent); });

var sqldb = sqlServer.AddDatabase("cardsdb");

var migrations = builder.AddProject<CardGames_MigrationService>("migrations")
	.WithReference(sqldb)
	.WaitFor(sqldb);

var internalIdentitySigningKey = builder.AddParameter("internalIdentitySigningKey", secret: true);

var api = builder.AddProject<CardGames_Poker_Api>("api")
	.WithEnvironment("InternalIdentity__SigningKey", internalIdentitySigningKey)
	.WithEnvironment("OTEL_SERVICE_NAME", "cardgames-poker-api")
	.WithReference(serviceBus)
	.WaitFor(serviceBus)
	.WithReference(cache)
	.WaitFor(cache)
	.WithReference(blobs)
	.WaitFor(blobs)
	.WithReference(sqldb)
	.WaitFor(sqldb)
	.WithReference(migrations)
	.WaitForCompletion(migrations);

var web = builder.AddProject<CardGames_Poker_Web>("web")
	.WithEnvironment("InternalIdentity__SigningKey", internalIdentitySigningKey)
	.WithReference(sqldb)
	.WaitFor(sqldb)
	.WithReference(api)
	.WaitFor(api);

//var options = new DevTunnelOptions
//{
//	Description = "localhost environment tunnel",
//	Labels = new[] { "qa", "testing" }.ToList(),
//	AllowAnonymous = false
//};
//var tunnel = builder.AddDevTunnel("poker-tunnel", "poker-tunnel", options)
//	.WithReference(web)
//	.WithReference(api)
//	.WithAnonymousAccess()
//	.WaitFor(web);

//var cli = builder.AddProject<Projects.CardGames_Poker_CLI>("cli")
//	.WithReference(api)
//	.WaitFor(api);

builder.Build().Run();