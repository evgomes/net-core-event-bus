using System;

namespace NetCoreEventBus.Infra.EventBus.Events
{
	/// <summary>
	/// Represents an integration event. 
	/// An Event is “something that has happened in the past”. An Integration Event is an event that can cause side effects to other microsrvices.
	/// </summary>
	public abstract class Event
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public DateTime CreatedAt { get; set; } = DateTime.Now;
	}
}
