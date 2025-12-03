using Asp.Versioning;
using CardGames.Poker.Api.Features;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using JasperFx;
using Marten;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Identity.Web;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDataSource("marten");

// Add services to the container.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization();
builder.Services.AddCors();
builder.Services.AddResponseCompression();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddValidation();
builder.Services.AddValidatorsFromAssemblyContaining<IValidationMarker>();
builder.Services.AddFluentValidationAutoValidation();

builder.AddRedisDistributedCache("cache");

builder.Services.AddFusionCache()
	.WithSerializer(new FusionCacheSystemTextJsonSerializer())
	.WithDefaultEntryOptions(options => options.Duration = TimeSpan.FromMinutes(5))
	.WithRegisteredDistributedCache()
	.AsHybridCache();

builder.Logging.AddOpenTelemetry(x =>
{
	x.IncludeScopes = true;
	x.IncludeFormattedMessage = true;
});

builder.Services.AddOpenTelemetry()
	.WithTracing(x =>
	{
		if (builder.Environment.IsDevelopment())
		{
			x.SetSampler<AlwaysOnSampler>();
		}

		x.AddFusionCacheInstrumentation(o =>
		{
			o.IncludeMemoryLevel = true;
		});

		x.SetResourceBuilder(ResourceBuilder.CreateDefault()
		 .AddService("api"))
		 .AddAspNetCoreInstrumentation() // For HTTP request tracing
		 .AddHttpClientInstrumentation() // For outgoing HTTP calls
		 .AddConsoleExporter(); // Use other exporters as needed
		
		x.AddSource("Azure.Messaging.ServiceBus");
	})
	.WithMetrics(x =>
	{
		x.AddPrometheusExporter();
		x.AddMeter("Forkful.ApiService");
		x.SetResourceBuilder(ResourceBuilder.CreateDefault()
			.AddService("api"))
			.AddAspNetCoreInstrumentation() // Captures request metrics
			.AddConsoleExporter(); // Export metrics to the console

		x.AddFusionCacheInstrumentation(o =>
		{
			o.IncludeMemoryLevel = true;
			o.IncludeDistributedLevel = true;
			o.IncludeBackplane = true;
		});
	});

AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());
builder.Services.AddMetrics();
builder.Services.AddRateLimiter(limiterOptions => limiterOptions
	.AddFixedWindowLimiter(policyName: "fixed", options =>
	{
		options.PermitLimit = 4;
		options.Window = TimeSpan.FromSeconds(12);
		options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
		options.QueueLimit = 2;
	}));
builder.Services.AddProblemDetails().AddErrorObjects();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


builder.Services.AddApiVersioning(
		options =>
		{
			options.ApiVersionReader = ApiVersionReader.Combine(
				new UrlSegmentApiVersionReader(),
				new HeaderApiVersionReader("api-version"),
				new QueryStringApiVersionReader("api-version")
			);

			options.DefaultApiVersion = new ApiVersion(1, 0);
			options.AssumeDefaultVersionWhenUnspecified = true;

			// reporting api versions will return the headers
			// "api-supported-versions" and "api-deprecated-versions"
			options.ReportApiVersions = true;

			options.Policies.Sunset(0.9)
				.Effective(DateTimeOffset.Now.AddDays(60))
				.Link("policy.html")
				.Title("Versioning Policy")
				.Type("text/html");

			options.ApiVersionSelector = new DefaultApiVersionSelector(options);
		})
	.AddApiExplorer(
		options =>
		{
			// add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
			// note: the specified format code will format the version as "'v'major[.minor][-status]"
			options.GroupNameFormat = "'v'VVV";

			// note: this option is only necessary when versioning by url segment. the SubstitutionFormat
			// can also be used to control the format of the API version in route templates
			options.SubstituteApiVersionInUrl = true;
		})
	// this enables binding ApiVersion as a endpoint callback parameter. if you don't use it, then
	// you should remove this configuration.
	.EnableApiVersionBinding();

builder.Services.AddMarten(opts =>
	{
		var connectionString = builder.Configuration.GetConnectionString("Marten");
		opts.Connection(connectionString);
		opts.DatabaseSchemaName = "incidents";
	})

// This adds configuration with Wolverine's transactional outbox and
// Marten middleware support to Wolverine
	.IntegrateWithWolverine();


builder.Host.UseWolverine(opts =>
{
	// This is almost an automatic default to have
	// Wolverine apply transactional middleware to any
	// endpoint or handler that uses persistence services
	opts.Policies.AutoApplyTransactions();
});

// To add Wolverine.HTTP services to the IoC container
builder.Services.AddWolverineHttp();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapWolverineEndpoints();
app.AddFeatureEndpoints();

app.UseStaticFiles(new StaticFileOptions
{
	FileProvider = new PhysicalFileProvider(
		Path.Combine(Directory.GetCurrentDirectory(), "swagger-ui")),
	RequestPath = "/swagger-ui"
});

app.UseResponseCompression();
app.MapPrometheusScrapingEndpoint();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.MapScalarApiReference(o =>
	o.WithTheme(ScalarTheme.Moon)
);
app.UseRateLimiter();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<TrimStringsMiddleware>();
app.UseHttpsRedirection();
app.UseHealthChecks("/health");


app.MapGet("/", () => "Card Games");


app.UseAuthentication();
app.UseAuthorization();

return await app.RunJasperFxCommands(args);

