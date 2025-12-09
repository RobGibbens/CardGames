using Aspire.Hosting.Azure;
using CardGames.Poker.Events;

namespace CardGames.AppHost;

public static class ServiceBusExtensions
{
	public static IResourceBuilder<AzureServiceBusResource> AddAndConfigureServiceBus(this IDistributedApplicationBuilder builder)
	{
		AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

		//Service Bus
		var serviceBus = builder
			.AddAzureServiceBus("messaging")
			.RunAsEmulator();
		IReadOnlyList<string> topics = TopicNames.GetTopics(typeof(TopicNames));

		foreach (var topic in topics)
		{
			var subscriptions = TopicNames.GetStringValuesUnderChild(typeof(TopicNames), topic);

			if (subscriptions.Any())
			{
				var serviceBusTopic = serviceBus.AddServiceBusTopic(topic);

				foreach (var subscription in subscriptions)
				{
					if (!string.IsNullOrWhiteSpace(subscription))
					{
						serviceBusTopic.AddServiceBusSubscription(subscription);
					}
				}
			}
		}

		return serviceBus;
	}
}
