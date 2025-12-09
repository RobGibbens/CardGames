namespace CardGames.Poker.Api.Infrastructure.Events;

public interface IServiceBusPublisher
{
	Task PublishMessageAsync<T>(T message, string topicName);
}