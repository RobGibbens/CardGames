using System.Reflection;
using Asp.Versioning;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Features;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Identity.Web;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;
using CardGames.Poker.Api.Infrastructure.PipelineBehaviors;
using MediatR;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;
using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Api.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddAuthentication(options =>
	{
		// Default to JWT for API endpoints
		options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
		options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
	})
	.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// Add header-based auth scheme for SignalR from Blazor frontend (separate registration)
builder.Services.AddAuthentication()
	.AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(
		HeaderAuthenticationHandler.SchemeName, _ => { });

// Configure authorization with multiple schemes
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Configure CORS for SignalR (requires credentials support)
builder.Services.AddCors(options =>
{
	options.AddPolicy("SignalRPolicy", policy =>
	{
		policy.SetIsOriginAllowed(_ => true) // Allow any origin for development
			.AllowAnyMethod()
			.AllowAnyHeader()
			.AllowCredentials();
	});
});
builder.Services.AddResponseCompression();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddValidation();
builder.Services.AddValidatorsFromAssemblyContaining<IValidationMarker>();
builder.Services.AddFluentValidationAutoValidation();

// Add SignalR services with JSON options to handle circular references
builder.Services.AddSignalR()
		.AddJsonProtocol(options =>
		{
			options.PayloadSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
			options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
		});
	builder.Services.AddSingleton<IUserIdProvider, SignalRUserIdProvider>();
	builder.Services.AddSingleton<IActionTimerService, ActionTimerService>();
	builder.Services.AddSingleton<IAutoActionService, AutoActionService>();
	builder.Services.AddScoped<ITableStateBuilder, TableStateBuilder>();
	builder.Services.AddScoped<IGameStateBroadcaster, GameStateBroadcaster>();
	builder.Services.AddScoped<ILobbyBroadcaster, LobbyBroadcaster>();
	builder.Services.AddScoped<IHandHistoryRecorder, HandHistoryRecorder>();

	// Add game flow handler factory for generic command handler architecture
	builder.Services.AddSingleton<IGameFlowHandlerFactory, GameFlowHandlerFactory>();

	// Add background service for continuous play (auto-start next hands)
	builder.Services.AddHostedService<ContinuousPlayBackgroundService>();

builder.AddRedisDistributedCache("cache");

builder.AddSqlServerDbContext<CardsDbContext>("cardsdb");

builder.Services.AddFusionCache()
	.WithSerializer(new FusionCacheSystemTextJsonSerializer(new System.Text.Json.JsonSerializerOptions
	{
		ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
		PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
	}))
	//TODO:ROB = .WithDefaultEntryOptions(options => options.Duration = TimeSpan.FromMinutes(5))
	.WithDefaultEntryOptions(options => options.Duration = TimeSpan.FromMilliseconds(2))
	.WithRegisteredDistributedCache()
	.AsHybridCache();

builder.Logging.AddOpenTelemetry(x =>
{
	x.IncludeScopes = true;
	x.IncludeFormattedMessage = true;
});


builder.Services.AddMediatR(cfg =>
	{
		cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
		cfg.LicenseKey = "eyJhbGciOiJSUzI1NiIsImtpZCI6Ikx1Y2t5UGVubnlTb2Z0d2FyZUxpY2Vuc2VLZXkvYmJiMTNhY2I1OTkwNGQ4OWI0Y2IxYzg1ZjA4OGNjZjkiLCJ0eXAiOiJKV1QifQ.eyJpc3MiOiJodHRwczovL2x1Y2t5cGVubnlzb2Z0d2FyZS5jb20iLCJhdWQiOiJMdWNreVBlbm55U29mdHdhcmUiLCJleHAiOiIxNzg2NTc5MjAwIiwiaWF0IjoiMTc1NTA1Mjg0NSIsImFjY291bnRfaWQiOiIwMTk3ZDFlN2Q4ZmQ3NWRjYmIwODA1OWVlZGEyZDU0ZCIsImN1c3RvbWVyX2lkIjoiY3RtXzAxano4eWdiMmJ4cTY3N2VhdmNqcmR6cTg1Iiwic3ViX2lkIjoiLSIsImVkaXRpb24iOiIwIiwidHlwZSI6IjIifQ.CpYPuTeHrEmaVt4v2sk5dDH9UGc-QkA7ThBphJcUFA8jr27yDsvD6Ts_CYiySDRzEQdbXeorGfx2ce1Ue5vZ8pk3c7qAj717cU-BP4qdk18dz1LXXkDgQj6v61hvg3OTdGUfV6wxR0Mq2NITiKIPz7q5v052KDXfaXSlwECSRTGwVGuTrn8q0JpcKuS8ZtcM9x32YiEXyiFR3f4cPiMePLMZvOZO-TOeMdVHUJDbAxbra-j5nScamIWbpnrlp4-8SQLYo9VR8xajnmQAiql6Vc5Zk_sKziZSmYcQmr52BFfP5K15STtgA3u9YQ4qQMNFcQ9jEK0BnTFCE45iHvcC3A";
	})
	.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>))
	.AddScoped(typeof(IPipelineBehavior<,>), typeof(GameStateBroadcastingBehavior<,>))
	.AddScoped(typeof(IPipelineBehavior<,>), typeof(LobbyStateBroadcastingBehavior<,>));

builder.Services.AddDistributedMemoryCache();

builder.Services.AddValidatorsFromAssembly(typeof(MapFeatureEndpoints).Assembly);
builder.AddAzureServiceBusClient(connectionName: "messaging");

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
builder.Services.AddOpenApi(options =>
{
	// Use endpoint name as operationId for cleaner API documentation
	options.AddOperationTransformer((operation, context, cancellationToken) =>
	{
		var endpointName = context.Description.ActionDescriptor.EndpointMetadata
			.OfType<EndpointNameAttribute>()
			.FirstOrDefault()?.EndpointName;

		if (!string.IsNullOrEmpty(endpointName))
		{
			operation.OperationId = endpointName;
		}

		return Task.CompletedTask;
	});
});

// Configure JSON serialization to support string enums and handle circular references
builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
	options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
	options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});


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

var app = builder.Build();

app.MapDefaultEndpoints();

app.AddFeatureEndpoints();

app.UseStaticFiles(new StaticFileOptions
{
	FileProvider = new PhysicalFileProvider(
		Path.Combine(Directory.GetCurrentDirectory(), "swagger-ui")),
	RequestPath = "/swagger-ui"
});

app.UseResponseCompression();
app.MapPrometheusScrapingEndpoint();
app.UseCors("SignalRPolicy");
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.MapScalarApiReference(o =>
	o.WithTheme(ScalarTheme.Alternate)
);
app.UseRateLimiter();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<TrimStringsMiddleware>();
app.UseHttpsRedirection();
app.UseHealthChecks("/health");


app.MapGet("/", () => "Card Games");


app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hubs with header-based auth for Blazor clients
app.MapHub<GameHub>("/hubs/game");
app.MapHub<LobbyHub>("/hubs/lobby");

app.Run();

