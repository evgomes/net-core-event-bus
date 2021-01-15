using NetCoreEventBus.Infra.EventBus.Bus;
using NetCoreEventBus.Infra.EventBus.Events;
using NetCoreEventBus.Infra.EventBus.RabbitMQ.Connection;
using NetCoreEventBus.Infra.EventBus.Subscriptions;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NetCoreEventBus.Infra.EventBus.RabbitMQ.Bus
{
	/// <summary>
	/// Event Bus implementation that uses RabbitMQ as the message broker.
	/// The implementation is based on eShopOnContainers (Microsoft's tutorial about microservices in .NET Core).
	/// 
	/// References:
	/// - https://docs.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/integration-event-based-microservice-communications
	/// - https://docs.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/rabbitmq-event-bus-development-test-environment
	/// </summary>
	public class RabbitMQEventBus : IEventBus
	{
		private readonly string _brokerName;
		private readonly string _queueName;

		private readonly IPersistentConnection _persistentConnection;
		private readonly IEventBusSubscriptionManager _subscriptionsManager;
		private readonly IServiceProvider _serviceProvider;

		private readonly int _retryCount;

		private IModel _consumerChannel;

		public RabbitMQEventBus(
			IPersistentConnection persistentConnection,
			IEventBusSubscriptionManager subscriptionsManager,
			IServiceProvider serviceProvider,
			string brokerName,
			string queueName,
			int retryCount = 5)
		{
			_persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
			_subscriptionsManager = subscriptionsManager ?? throw new ArgumentNullException(nameof(subscriptionsManager));
			_serviceProvider = serviceProvider;
			_brokerName = brokerName ?? throw new ArgumentNullException(nameof(brokerName));
			_queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
			_retryCount = retryCount;

			ConfigureMessageBroker();
		}

		public void Publish<TEvent>(TEvent @event)
			where TEvent : Event
		{
			if (!_persistentConnection.IsConnected)
			{
				_persistentConnection.TryConnect();
			}

			var policy = Policy
				.Handle<BrokerUnreachableException>()
				.Or<SocketException>()
				.WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

			var eventName = @event.GetType().Name;

			using (var channel = _persistentConnection.CreateModel())
			{
				channel.ExchangeDeclare(exchange: _brokerName, type: "direct");

				var message = JsonSerializer.Serialize<TEvent>(@event);
				var body = Encoding.UTF8.GetBytes(message);

				policy.Execute(() =>
				{
					var properties = channel.CreateBasicProperties();
					properties.DeliveryMode = (byte)DeliveryMode.Persistent;

					channel.BasicPublish(
						exchange: _brokerName,
						routingKey: eventName,
						mandatory: true,
						basicProperties: properties,
						body: body);
				});
			}
		}

		public void Subscribe<TEvent, TEventHandler>()
			where TEvent : Event
			where TEventHandler : IEventHandler<TEvent>
		{
			var eventName = _subscriptionsManager.GetEventIdentifier<TEvent>();
			AddQueueBindForEventSubscription(eventName);

			_subscriptionsManager.AddSubscription<TEvent, TEventHandler>();
			StartBasicConsume();
		}

		public void Unsubscribe<TEvent, TEventHandler>()
			where TEvent : Event
			where TEventHandler : IEventHandler<TEvent>
		{
			_subscriptionsManager.RemoveSubscription<TEvent, TEventHandler>();
		}

		private void ConfigureMessageBroker()
		{
			_consumerChannel = CreateConsumerChannel();
			_subscriptionsManager.OnEventRemoved += SubscriptionManager_OnEventRemoved;
		}

		private IModel CreateConsumerChannel()
		{
			if (!_persistentConnection.IsConnected)
			{
				_persistentConnection.TryConnect();
			}

			var channel = _persistentConnection.CreateModel();

			channel.ExchangeDeclare(exchange: _brokerName, type: "direct");
			channel.QueueDeclare(
				queue: _queueName,
				durable: true,
				exclusive: false,
				autoDelete: false,
				arguments: null);

			channel.CallbackException += (sender, ea) =>
			{
				_consumerChannel.Dispose();
				_consumerChannel = CreateConsumerChannel();
				StartBasicConsume();
			};

			return channel;
		}

		private void StartBasicConsume()
		{
			if (_consumerChannel == null)
			{
				return;
			}

			var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

			consumer.Received += Consumer_Received;

			_consumerChannel.BasicConsume(
				queue: _queueName,
				autoAck: false,
				consumer: consumer);
		}

		private async Task Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
		{
			var eventName = eventArgs.RoutingKey;
			var message = Encoding.UTF8.GetString(eventArgs.Body.Span);

			await ProcessEvent(eventName, message);

			_consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
		}

		private async Task ProcessEvent(string eventName, string message)
		{
			if (!_subscriptionsManager.HasSubscriptionsForEvent(eventName))
			{
				return;
			}

			var subscriptions = _subscriptionsManager.GetHandlersForEvent(eventName);
			foreach (var subscription in subscriptions)
			{
				var handler = _serviceProvider.GetService(subscription.HandlerType);
				if (handler == null)
				{
					continue;
				}

				var eventType = _subscriptionsManager.GetEventTypeByName(eventName);

				var	@event = JsonSerializer.Deserialize(message, eventType);
				var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);
				await Task.Yield();
				await (Task)concreteType.GetMethod(nameof(IEventHandler<Event>.HandleAsync)).Invoke(handler, new object[] { @event });
			}
		}

		private void SubscriptionManager_OnEventRemoved(object sender, string eventName)
		{
			if (!_persistentConnection.IsConnected)
			{
				_persistentConnection.TryConnect();
			}

			using (var channel = _persistentConnection.CreateModel())
			{
				channel.QueueUnbind(queue: _queueName, exchange: _brokerName, routingKey: eventName);

				if (_subscriptionsManager.IsEmpty)
				{
					_consumerChannel.Close();
				}
			}
		}

		private void AddQueueBindForEventSubscription(string eventName)
		{
			var containsKey = _subscriptionsManager.HasSubscriptionsForEvent(eventName);
			if (containsKey)
			{
				return;
			}

			if (!_persistentConnection.IsConnected)
			{
				_persistentConnection.TryConnect();
			}

			using (var channel = _persistentConnection.CreateModel())
			{
				channel.QueueBind(queue: _queueName, exchange: _brokerName, routingKey: eventName);
			}
		}
	}
}
