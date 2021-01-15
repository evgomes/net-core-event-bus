using NetCoreEventBus.Infra.EventBus.Events;

namespace NetCoreEventBus.Web.IntegrationEvents.Events
{
	// Integration Events notes (from eShopOnContainers sample code): 
	// An Event is “something that has happened in the past”. An Integration Event is an event that can cause side effects to other microsrvices, 
	// Bounded-Contexts or external systems.
	public class MessageSentEvent : Event
	{
		public string Message { get; set; }

		public override string ToString()
		{
			return $"ID: {Id} - Created at: {CreatedAt:MM/dd/yyyy} - Message: {Message}";
		}
	}
}
