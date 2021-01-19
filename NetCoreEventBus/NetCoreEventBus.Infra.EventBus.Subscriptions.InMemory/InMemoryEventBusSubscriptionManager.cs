using NetCoreEventBus.Infra.EventBus.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NetCoreEventBus.Infra.EventBus.Subscriptions.InMemory
{
	public class InMemoryEventBusSubscriptionManager : IEventBusSubscriptionManager
	{
		#region Fields
		private readonly Dictionary<string, List<Subscription>> _handlers = new Dictionary<string, List<Subscription>>();
		private readonly List<Type> _eventTypes = new List<Type>();
		#endregion

		#region Event Handlers
		public event EventHandler<string> OnEventRemoved;
		#endregion

		#region Events info
		public string GetEventIdentifier<TEvent>() => typeof(TEvent).Name;

		public Type GetEventTypeByName(string eventName) => _eventTypes.SingleOrDefault(t => t.Name == eventName);

		public IEnumerable<Subscription> GetHandlersForEvent(string eventName) => _handlers[eventName];

		/// <summary>
		/// Returns the dictionary of subscriptiosn in an immutable way.
		/// </summary>
		/// <returns>Dictionary.</returns>
		public Dictionary<string, List<Subscription>> GetAllSubscriptions() => new Dictionary<string, List<Subscription>>(_handlers);
		#endregion

		#region Subscriptions management
		public void AddSubscription<TEvent, TEventHandler>()
			where TEvent : Event
			where TEventHandler : IEventHandler<TEvent>
		{
			var eventName = GetEventIdentifier<TEvent>();

			DoAddSubscription(typeof(TEvent), typeof(TEventHandler), eventName);

			if (!_eventTypes.Contains(typeof(TEvent)))
			{
				_eventTypes.Add(typeof(TEvent));
			}
		}

		public void RemoveSubscription<TEvent, TEventHandler>()
			where TEventHandler : IEventHandler<TEvent>
			where TEvent : Event
		{
			var handlerToRemove = FindSubscriptionToRemove<TEvent, TEventHandler>();
			var eventName = GetEventIdentifier<TEvent>();
			DoRemoveHandler(eventName, handlerToRemove);
		}

		public void Clear()
		{
			_handlers.Clear();
			_eventTypes.Clear();
		}
		#endregion

		#region Status
		public bool IsEmpty => !_handlers.Keys.Any();

		public bool HasSubscriptionsForEvent(string eventName) => _handlers.ContainsKey(eventName);
		#endregion

		#region Private methods
		private void DoAddSubscription(Type eventType, Type handlerType, string eventName)
		{
			if (!HasSubscriptionsForEvent(eventName))
			{
				_handlers.Add(eventName, new List<Subscription>());
			}

			if (_handlers[eventName].Any(s => s.HandlerType == handlerType))
			{
				throw new ArgumentException($"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
			}

			_handlers[eventName].Add(new Subscription(eventType, handlerType));
		}

		private void DoRemoveHandler(string eventName, Subscription subscriptionToRemove)
		{
			if (subscriptionToRemove == null)
			{
				return;
			}

			_handlers[eventName].Remove(subscriptionToRemove);
			if (_handlers[eventName].Any())
			{
				return;
			}

			_handlers.Remove(eventName);
			var eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);
			if (eventType != null)
			{
				_eventTypes.Remove(eventType);
			}

			RaiseOnEventRemoved(eventName);
		}

		private void RaiseOnEventRemoved(string eventName)
		{
			var handler = OnEventRemoved;
			handler?.Invoke(this, eventName);
		}

		private Subscription FindSubscriptionToRemove<TEvent, TEventHandler>()
			 where TEvent : Event
			 where TEventHandler : IEventHandler<TEvent>
		{
			var eventName = GetEventIdentifier<TEvent>();
			return DoFindSubscriptionToRemove(eventName, typeof(TEventHandler));
		}

		private Subscription DoFindSubscriptionToRemove(string eventName, Type handlerType)
		{
			if (!HasSubscriptionsForEvent(eventName))
			{
				return null;
			}

			return _handlers[eventName].SingleOrDefault(s => s.HandlerType == handlerType);

		}
		#endregion
	}
}
