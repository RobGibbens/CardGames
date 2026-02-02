using CardGames.Poker.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

// Alias Program to avoid conflict
using ApiProgram = CardGames.Poker.Api.Program;

namespace CardGames.IntegrationTests.Infrastructure;

public class ApiWebApplicationFactory : WebApplicationFactory<ApiProgram>
{
    private readonly string _databaseName = $"ApiTestDb_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Provide dummy connection string to satisfy basic validation if cleanup misses something
        builder.UseSetting("ConnectionStrings:cardsdb", "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;ConnectRetryCount=0");

        builder.ConfigureTestServices(services =>
        {
            // Remove the app's DbContext registration and pooling
            // Also serve to remove the Aspire configuration options that validate connection strings
            var descriptorsToRemove = services.Where(d =>
            {
                var type = d.ServiceType;
                
                // Direct Context and Options matches
                if (type == typeof(DbContextOptions<CardsDbContext>) ||
                    type == typeof(DbContextOptions) || 
                    type == typeof(CardsDbContext))
                {
                    return true;
                }

                // Generic Infrastructure (Pools, Leases)
                if (type.FullName != null && 
                   (type.FullName.Contains("Microsoft.EntityFrameworkCore.Internal.IDbContextPool") || 
                    type.FullName.Contains("Microsoft.EntityFrameworkCore.Internal.IScopedDbContextLease")))
                {
                    return true;
                }

                // Configuration Options (IConfigureOptions<>, IPostConfigureOptions<>, etc.)
                // These are where UseSqlServer calls are hiding
                if (type.IsGenericType)
                {
                    var genericDef = type.GetGenericTypeDefinition();
                    if (genericDef == typeof(IConfigureOptions<>) || 
                        genericDef == typeof(IPostConfigureOptions<>) ||
                        genericDef == typeof(IValidateOptions<>))
                    {
                        var arg = type.GenericTypeArguments.FirstOrDefault();
                        // Check if the configuration target is related to our DbContext
                        if (arg != null && (
                            arg == typeof(DbContextOptions<CardsDbContext>) || 
                            arg == typeof(DbContextOptions) ||
                            arg == typeof(CardsDbContext)
                        ))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }).ToList();

            foreach (var d in descriptorsToRemove)
            {
                services.Remove(d);
            }
            
            // Allow resolving IDbContextPool if some internal service still insists on it, but backed by standard context?
            // No, removing the IScopedDbContextLease should break the chain requiring IDbContextPool.
            
            // Note: If using Aspire defaults or AddDbContextPool, EF adds:
            // - Scoped: CardsDbContext (resolves from lease)
            // - Scoped: IScopedDbContextLease<CardsDbContext>
            // - Singleton: IDbContextPool<CardsDbContext>
            // - Scoped: DbContextOptions<CardsDbContext>
            
            // We must remove ALL of these before adding our own.
            
            // Add a database context using an in-memory database for testing.
            services.AddDbContext<CardsDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.EnableSensitiveDataLogging();
            });
            
            // If we need to bypass authentication, we can add a custom handler here.
            // For now, we'll assume endpoints are open or we'll handle auth later.
        });
    }
}
