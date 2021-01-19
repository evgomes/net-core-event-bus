using NetCoreEventBus.Infra.EventBus.Events;
using System;
using System.Collections.Generic;

namespace NetCoreEventBus.Infra.EventBus.Subscriptions
{
	/// <summary>
	/// Contract that defines how events are tracked in the application.
	/// The implementation of this class controls the current subscriptions, as well as resolve event handlers for usage.
	/// </summary>
	public interface IEventBusSubscriptionManager
	{
		#region Event Handlers
		event EventHandler<string> OnEventRemoved;
		#endregion

		#region Status
		bool IsEmpty { get; }
		bool HasSubscriptionsForEvent(string eventName);
		#endregion

		#region Events info
		string GetEventIdentifier<TEvent>();
		Type GetEventTypeByName(string eventName);
		IEnumerable<Subscription> GetHandlersForEvent(string eventName);
		Dictionary<string, List<Subscription>> GetAllSubscriptions();
		#endregion

		#region Subscription management
		void AddSubscription<TEvent, TEventHandler>()
			where TEvent : Event
			where TEventHandler : IEventHandler<TEvent>;

		void RemoveSubscription<TEvent, TEventHandler>()
			where TEvent : Event
			where TEventHandler : IEventHandler<TEvent>;

		void Clear();
		#endregion
	}
}
