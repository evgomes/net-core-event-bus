using RabbitMQ.Client;

namespace NetCoreEventBus.Infra.EventBus.RabbitMQ.Connection
{
	public interface IPersistentConnection
	{
		bool IsConnected { get; }
		bool TryConnect();
		IModel CreateModel();
	}
}
