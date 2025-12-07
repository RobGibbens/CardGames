using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace CardGames.Poker.Api.Infrastructure.Events;

public class ServiceBusPublisher : IServiceBusPublisher, IAsyncDisposable
{
	private readonly ServiceBusClient _client;
	private readonly ServiceBusAdministrationClient _adminClient;

	public ServiceBusPublisher(string connectionString)
	{
		_client = new ServiceBusClient(connectionString);
		_adminClient = new ServiceBusAdministrationClient(connectionString);
	}

	public async Task PublishMessageAsync<T>(T message, string queueName)
	{
		await EnsureQueueExistsAsync(queueName);
		var sender = _client.CreateSender(queueName);
		var messageBody = JsonSerializer.Serialize(message);
		var serviceBusMessage = new ServiceBusMessage(messageBody)
		{
			ContentType = "application/json",
			Subject = typeof(T).Name
		};

		try
		{
			await sender.SendMessageAsync(serviceBusMessage);
		}
		catch (Exception ex)
		{
			var m = ex.Message;
		}
		finally
		{
			await sender.DisposeAsync();
		}
	}

	private async Task EnsureQueueExistsAsync(string queueName)
	{
		if (!await _adminClient.QueueExistsAsync(queueName))
		{
			await _adminClient.CreateQueueAsync(queueName);
		}
	}

	public async ValueTask DisposeAsync()
	{
		await _client.DisposeAsync();
	}
}