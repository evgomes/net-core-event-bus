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
		private readonly TimeSpan _timeoutBeforeReconnecting;

		private IConnection _connection;
		private bool _disposed;

		private readonly object _locker = new object();

		private readonly ILogger<RabbitMQPersistentConnection> _logger;

		private bool _connectionFailed = false;

		public RabbitMQPersistentConnection
		(
			IConnectionFactory connectionFactory,
			ILogger<RabbitMQPersistentConnection> logger,
			int timeoutBeforeReconnecting = 15
		)
		{
			_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
			_logger = logger;
			_timeoutBeforeReconnecting = TimeSpan.FromSeconds(timeoutBeforeReconnecting);
		}

		public event EventHandler OnReconnectedAfterConnectionFailure;

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
				// Creates a policy to retry connecting to message broker until it succeds.
				var policy = Policy
					.Handle<SocketException>()
					.Or<BrokerUnreachableException>()
					.WaitAndRetryForever((duration) => _timeoutBeforeReconnecting, (ex, time) =>
					{
						_logger.LogWarning(ex, "RabbitMQ Client could not connect after {TimeOut} seconds ({ExceptionMessage}). Waiting to try again...", $"{(int)time.TotalSeconds}", ex.Message);
					});

				policy.Execute(() =>
				{
					_connection = _connectionFactory.CreateConnection();
				});

				if (!IsConnected)
				{
					_logger.LogCritical("ERROR: could not connect to RabbitMQ.");
					_connectionFailed = true;
					return false;
				}

				// These event handlers hadle situations where the connection is lost by any reason. They try to reconnect the client.
				_connection.ConnectionShutdown += OnConnectionShutdown;
				_connection.CallbackException += OnCallbackException;
				_connection.ConnectionBlocked += OnConnectionBlocked;
				_connection.ConnectionUnblocked += OnConnectionUnblocked;

				_logger.LogInformation("RabbitMQ Client acquired a persistent connection to '{HostName}' and is subscribed to failure events", _connection.Endpoint.HostName);

				// If the connection has failed previously because of a RabbitMQ shutdown or something similar, we need to guarantee that the exchange and queues exist again.
				// It's also necessary to rebind all application event handlers. We use this event handler below to do this.
				if (_connectionFailed)
				{
					OnReconnectedAfterConnectionFailure?.Invoke(this, null);
					_connectionFailed = false;
				}

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

		private void OnCallbackException(object sender, CallbackExceptionEventArgs args)
		{
			_connectionFailed = true;

			_logger.LogWarning("A RabbitMQ connection throw exception. Trying to re-connect...");
			TryConnectIfNotDisposed();
		}

		private void OnConnectionShutdown(object sender, ShutdownEventArgs args)
		{
			_connectionFailed = true;

			_logger.LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");
			TryConnectIfNotDisposed();
		}

		private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs args)
		{
			_connectionFailed = true;

			_logger.LogWarning("A RabbitMQ connection is blocked. Trying to re-connect...");
			TryConnectIfNotDisposed();
		}

		private void OnConnectionUnblocked(object sender, EventArgs args)
		{
			_connectionFailed = true;

			_logger.LogWarning("A RabbitMQ connection is unblocked. Trying to re-connect...");
			TryConnectIfNotDisposed();
		}

		private void TryConnectIfNotDisposed()
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
