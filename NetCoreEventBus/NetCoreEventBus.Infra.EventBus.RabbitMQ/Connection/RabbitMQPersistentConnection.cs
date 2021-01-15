using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Net.Sockets;

namespace NetCoreEventBus.Infra.EventBus.RabbitMQ.Connection
{
	/// <summary>
	/// Default RabbitMQ connection helper. Based on eShopOnContainers implementation.
	/// </summary>
	public class RabbitMQPersistentConnection : IPersistentConnection
	{
		private readonly IConnectionFactory _connectionFactory;
		private readonly int _retryCount;

		private IConnection _connection;
		private bool _disposed;

		private readonly object _locker = new object();

		public RabbitMQPersistentConnection(
			IConnectionFactory connectionFactory,
			int retryCount = 5)
		{
			_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
			_retryCount = retryCount;
		}

		public bool IsConnected
		{
			get
			{
				return (_connection != null) && (_connection.IsOpen) && (!_disposed);
			}
		}

		public bool TryConnect()
		{
			lock (_locker)
			{
				// Creates a policy to retry connecting to message broker a given number of times, defined on the constructor.
				var policy = Policy.Handle<SocketException>().Or<BrokerUnreachableException>().WaitAndRetry(_retryCount, duration => TimeSpan.FromSeconds(Math.Pow(2, duration)));
				policy.Execute(() => { _connection = _connectionFactory.CreateConnection(); });

				if (!IsConnected)
				{
					return false;
				}

				// Adds event handlers to common situations when using the message broker.
				_connection.ConnectionShutdown += OnConnectionShutdown;
				_connection.CallbackException += OnCallbackException;
				_connection.ConnectionBlocked += OnConnectionBlocked;

				return true;
			}
		}

		public IModel CreateModel()
		{
			if (!IsConnected)
			{
				throw new InvalidOperationException("No RabbitMQ connections are available to perform this action.");
			}

			return _connection.CreateModel();
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_connection.Dispose();
		}

		private void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
		{
			TryConnectWhenNotDisposed();
		}

		private void OnCallbackException(object sender, CallbackExceptionEventArgs e)
		{
			TryConnectWhenNotDisposed();
		}

		private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
		{
			TryConnectWhenNotDisposed();
		}

		private void TryConnectWhenNotDisposed()
		{
			if (_disposed)
			{
				return;
			}

			TryConnect();
		}
	}
}
