using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.IO;
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

		private readonly ILogger<RabbitMQPersistentConnection> _logger;

		public RabbitMQPersistentConnection(
			IConnectionFactory connectionFactory,
			ILogger<RabbitMQPersistentConnection> logger,
			int retryCount = 5)
		{
			_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
			_logger = logger;
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
			_logger.LogInformation("Trying to connect to RabbitMQ...");

			lock (_locker)
			{
				// Creates a policy to retry connecting to message broker a given number of times, defined on the constructor.
				var policy = Policy
					.Handle<SocketException>()
					.Or<BrokerUnreachableException>()
					.WaitAndRetry(_retryCount, duration => TimeSpan.FromSeconds(Math.Pow(2, duration)), (ex, time) =>
					{
						_logger.LogWarning(ex, "RabbitMQ Client could not connect after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
					});

				policy.Execute(() => { _connection = _connectionFactory.CreateConnection(); });

				if (!IsConnected)
				{
					_logger.LogCritical("ERROR: could not connect to RabbitMQ.");
					return false;
				}

				// Adds event handlers to common situations when using the message broker.
				_connection.ConnectionShutdown += OnConnectionShutdown;
				_connection.CallbackException += OnCallbackException;
				_connection.ConnectionBlocked += OnConnectionBlocked;

				_logger.LogInformation("RabbitMQ Client acquired a persistent connection to '{HostName}' and is subscribed to failure events", _connection.Endpoint.HostName);

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

			try
			{
				_connection.Dispose();
			}
			catch (IOException ex)
			{
				_logger.LogCritical(ex.ToString());
			}
		}

		private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
		{
			_logger.LogWarning("A RabbitMQ connection is shutdown. Trying to re-connect...");
			TryConnectWhenNotDisposed();
		}

		private void OnCallbackException(object sender, CallbackExceptionEventArgs e)
		{

			_logger.LogWarning("A RabbitMQ connection throw exception. Trying to re-connect...");
			TryConnectWhenNotDisposed();
		}

		private void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
		{
			_logger.LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");
			TryConnectWhenNotDisposed();
		}

		private void TryConnectWhenNotDisposed()
		{
			if (_disposed)
			{
				_logger.LogInformation("RabbitMQ client is disposed. No action will be taken.");
				return;
			}

			TryConnect();
		}
	}
}
