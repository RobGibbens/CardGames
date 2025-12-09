namespace CardGames.Poker.Api.Infrastructure.Events;

public static class ServiceBusExtensions
{
	public static IServiceCollection AddServiceBusPublisher(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddSingleton<IServiceBusPublisher>(sp =>
		{
			var connectionString = configuration["ConnectionStrings:messaging"];
			//var connectionString = configuration.GetValue<string>("AzureServiceBus:ConnectionString");
			return new ServiceBusPublisher(connectionString);
		});

		return services;
	}
}