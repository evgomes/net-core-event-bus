using RabbitMQ.Client;
using System;

namespace NetCoreEventBus.Infra.EventBus.RabbitMQ.Connection
{
	public interface IPersistentConnection
	{
		event EventHandler OnReconnectedAfterConnectionFailure;
		bool IsConnected { get; }

		bool TryConnect();
		IModel CreateModel();
	}
}
