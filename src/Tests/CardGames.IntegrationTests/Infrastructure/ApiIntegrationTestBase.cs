using System.Net.Http.Json;
using CardGames.Poker.Api.Data;
using Microsoft.Extensions.DependencyInjection;

namespace CardGames.IntegrationTests.Infrastructure;

[Collection("ApiTests")] // Avoid parallel execution if sharing resources, though we are using unique DBs per factory instance? 
// Wait, WebApplicationFactory is usually created per test class or shared.
// If I use the same factory instance for all tests, they share the DB name. 
// I should create a new factory per test or per class.
public abstract class ApiIntegrationTestBase : IClassFixture<ApiWebApplicationFactory>, IAsyncLifetime
{
    protected readonly ApiWebApplicationFactory Factory;
    protected HttpClient Client;
    protected IServiceScope Scope;
    protected CardsDbContext DbContext;

    protected ApiIntegrationTestBase(ApiWebApplicationFactory factory)
    {
        // We need a fresh factory for fresh DB? 
        // Providing Factory via IClassFixture shares it across tests in the class.
        // But the factory has a fixed DB name in constructor.
        // So tests in the same class share the DB. 
        // This is fine for read tests, but for write tests we might want isolation.
        // Given complexity, let's share for now and clean up if needed, or rely on distinct data.
        Factory = factory;
        Client = factory.CreateClient();
    }

    public virtual async Task InitializeAsync()
    {
        Scope = Factory.Services.CreateScope();
        DbContext = Scope.ServiceProvider.GetRequiredService<CardsDbContext>();
        await DbContext.Database.EnsureCreatedAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (DbContext != null)
        {
            await DbContext.Database.EnsureDeletedAsync();
            await DbContext.DisposeAsync();
        }
        Scope?.Dispose();
    }

    // Helper methods
    protected async Task<T?> GetAsync<T>(string url)
    {
        return await Client.GetFromJsonAsync<T>(url);
    }

    protected async Task<HttpResponseMessage> PostAsync<T>(string url, T content)
    {
        return await Client.PostAsJsonAsync(url, content);
    }
}
