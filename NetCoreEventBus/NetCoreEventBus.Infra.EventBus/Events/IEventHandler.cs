using System.Threading.Tasks;

namespace NetCoreEventBus.Infra.EventBus.Events
{
	/// <summary>
	/// Contract for event handlers. Event handlers are responsible for processing events when they happen.
	/// </summary>
	/// <typeparam name="TEvent">Event type.</typeparam>
	public interface IEventHandler<in TEvent>
		where TEvent : Event
	{
		Task HandleAsync(TEvent @event);
	}
}
