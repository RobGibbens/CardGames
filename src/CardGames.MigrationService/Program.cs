using CardGames.Poker.Api.Data;
using CardGames.Poker.MigrationService;


var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
	.WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddSqlServerDbContext<CardsDbContext>("cardsdb");

var host = builder.Build();
host.Run();
