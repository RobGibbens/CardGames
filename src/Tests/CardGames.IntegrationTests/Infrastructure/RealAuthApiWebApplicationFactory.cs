using CardGames.Poker.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using ApiProgram = CardGames.Poker.Api.Program;

namespace CardGames.IntegrationTests.Infrastructure;

public sealed class RealAuthApiWebApplicationFactory : WebApplicationFactory<ApiProgram>
{
    private readonly string _databaseName = $"RealAuthApiTestDb_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:cardsdb", "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;ConnectRetryCount=0");
        builder.UseSetting("ConnectionStrings:messaging", "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abcdefghijklmnopqrstuvwxyz0123456789=");
        builder.UseSetting("InternalApiAuth:Issuer", "CardGames.Internal");
        builder.UseSetting("InternalApiAuth:Audience", "CardGames.Poker.Api");
        builder.UseSetting("InternalApiAuth:SigningKey", "dev-only-cardgames-internal-api-signing-key-20260529");

        builder.ConfigureTestServices(services =>
        {
#pragma warning disable EXTEXP0018
            services.AddHybridCache();
#pragma warning restore EXTEXP0018

            var descriptorsToRemove = services.Where(d =>
            {
                var type = d.ServiceType;

                if (type == typeof(DbContextOptions<CardsDbContext>)
                    || type == typeof(DbContextOptions)
                    || type == typeof(CardsDbContext))
                {
                    return true;
                }

                if (type.FullName is not null
                    && (type.FullName.Contains("Microsoft.EntityFrameworkCore.Internal.IDbContextPool")
                        || type.FullName.Contains("Microsoft.EntityFrameworkCore.Internal.IScopedDbContextLease")))
                {
                    return true;
                }

                if (!type.IsGenericType)
                {
                    return false;
                }

                var genericDef = type.GetGenericTypeDefinition();
                if (genericDef != typeof(IConfigureOptions<>)
                    && genericDef != typeof(IPostConfigureOptions<>)
                    && genericDef != typeof(IValidateOptions<>))
                {
                    return false;
                }

                var arg = type.GenericTypeArguments.FirstOrDefault();
                return arg == typeof(DbContextOptions<CardsDbContext>)
                    || arg == typeof(DbContextOptions)
                    || arg == typeof(CardsDbContext);
            }).ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<IDatabaseProvider>();
            services.RemoveAll<DbContextOptions<CardsDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<CardsDbContext>();
            services.RemoveAll<IDbContextFactory<CardsDbContext>>();
            services.RemoveAll(typeof(IConfigureOptions<DbContextOptions<CardsDbContext>>));
            services.RemoveAll(typeof(IPostConfigureOptions<DbContextOptions<CardsDbContext>>));
            services.RemoveAll(typeof(IValidateOptions<DbContextOptions<CardsDbContext>>));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<CardsDbContext>));

            services.AddDbContext<CardsDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.EnableSensitiveDataLogging();
            });

            services.Configure<HealthCheckServiceOptions>(options =>
            {
                var checksToRemove = options.Registrations
                    .Where(registration => registration.Name != "self")
                    .ToList();

                foreach (var check in checksToRemove)
                {
                    options.Registrations.Remove(check);
                }
            });
        });
    }
}