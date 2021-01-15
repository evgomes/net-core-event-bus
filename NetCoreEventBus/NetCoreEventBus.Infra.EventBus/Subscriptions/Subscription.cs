using System;

namespace NetCoreEventBus.Infra.EventBus.Subscriptions
{
	/// <summary>
	/// Represents an event subscription. Subscriptions control when we listen to events.
	/// </summary>
	public class Subscription
	{
		public Type HandlerType { get; private set; }

		public Subscription(Type handlerType)
		{
			HandlerType = handlerType;
		}
	}
}
