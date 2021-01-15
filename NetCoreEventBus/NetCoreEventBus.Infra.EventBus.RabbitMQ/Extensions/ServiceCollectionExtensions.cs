using Microsoft.Extensions.DependencyInjection;
using NetCoreEventBus.Infra.EventBus.Bus;
using NetCoreEventBus.Infra.EventBus.RabbitMQ.Bus;
using NetCoreEventBus.Infra.EventBus.RabbitMQ.Connection;
using NetCoreEventBus.Infra.EventBus.Subscriptions;
using NetCoreEventBus.Infra.EventBus.Subscriptions.InMemory;
using RabbitMQ.Client;
using System;

namespace NetCoreEventBus.Infra.EventBus.RabbitMQ.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds an event bus that uses RabbitMQ to deliver messages.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="connectionUrl">URL to connect to RabbitMQ.</param>
        /// <param name="brokerName">Broker name. This represents the exchange name.</param>
        /// <param name="queueName">Messa queue name, to track on RabbitMQ.</param>
        /// <param name="connectionRetryCount">Connection retry count, for both connection to RabbitMQ, and for retrying the publish and  consume operations.</param>
		public static void AddRabbitMQEventBus(this IServiceCollection services, string connectionUrl, string brokerName, string queueName, int connectionRetryCount = 5)
        {
            services.AddSingleton<IEventBusSubscriptionManager, InMemoryEventBusSubscriptionManager>();
            services.AddSingleton<IPersistentConnection, RabbitMQPersistentConnection>(factory =>
            {
                var connectionFactory = new ConnectionFactory
                {
                    Uri = new Uri(connectionUrl),
                    DispatchConsumersAsync = true,
                };

                return new RabbitMQPersistentConnection(connectionFactory, connectionRetryCount);
            });

            services.AddSingleton<IEventBus, RabbitMQEventBus>(factory =>
            {
                var persistentConnection = factory.GetService<IPersistentConnection>();
                var subscriptionManager = factory.GetService<IEventBusSubscriptionManager>();

                return new RabbitMQEventBus(persistentConnection, subscriptionManager, factory, brokerName, queueName, connectionRetryCount);
            });
        }
    }
}
