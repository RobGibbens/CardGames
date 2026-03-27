using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.MigrationService;
using Microsoft.AspNetCore.Identity;


var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
	.WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddSqlServerDbContext<CardsDbContext>("cardsdb");

builder.Services.AddIdentityCore<ApplicationUser>(options =>
	{
		options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
	})
	.AddEntityFrameworkStores<CardsDbContext>();

var host = builder.Build();
host.Run();
