using Microsoft.AspNetCore.Mvc;
using NetCoreEventBus.Infra.EventBus.Bus;
using NetCoreEventBus.Web.IntegrationEvents.Events;
using System;

namespace NetCoreEventBus.Web.Controllers
{
	[ApiController]
	[Route("api/event-bus")]
	[Produces("application/json")]
	public class EventBusController : Controller
	{
		private readonly IEventBus _eventBus;

		public EventBusController(IEventBus eventBus)
		{
			_eventBus = eventBus;
		}

		/// <summary>
		/// Sends a message through the event bus. This route is here for testing purposes.
		/// </summary>
		/// <param name="message">Message to send.</param>
		/// <returns>Message sent confirmation.</returns>
		[HttpPost]
		[ProducesResponseType(typeof(string), 200)]
		public IActionResult SendMessage([FromBody] string message)
		{
			_eventBus.Publish(new MessageSentEvent { Message = message });
			return Ok("Message sent.");
		}
	}
}
