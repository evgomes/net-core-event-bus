using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using NetCoreEventBus.Infra.EventBus.Bus;
using NetCoreEventBus.Infra.EventBus.RabbitMQ.Extensions;
using NetCoreEventBus.Web.Controllers.Configurations;
using NetCoreEventBus.Web.IntegrationEvents.EventHandlers;
using NetCoreEventBus.Web.IntegrationEvents.Events;
using System;
using System.IO;
using System.Reflection;

namespace NetCoreEventBus.Web
{
	public class Startup
	{
		public IConfiguration Configuration { get; }

		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSwaggerGen(cfg =>
			{
				cfg.SwaggerDoc("v1", new OpenApiInfo
				{
					Title = "ASP.NET Core Event Bus API",
					Version = "v1",
					Description = "Sample API that shows how to send messaged through an event broker using RabbitMQ.",
					Contact = new OpenApiContact
					{
						Name = "Evandro Gayer Gomes",
						Url = new Uri("https://evandroggomes.com.br/en"),
						Email = "evandro.ggomes@outlook.com"
					},
				});

				var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
				var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
				cfg.IncludeXmlComments(xmlPath);
			});

			services
				.AddControllers()
				.ConfigureApiBehaviorOptions(opt =>
				{
					opt.InvalidModelStateResponseFactory = InvalidModelStateResponseFactory.ProduceErrorResponse;
				});

			// Here we configure the event bus
			ConfigureEventBusDependencies(services);
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}
			else
			{
				app.UseExceptionHandler("/Home/Error");
			}

			app.UseStaticFiles();

			app.UseSwagger().UseSwaggerUI(options =>
			{
				options.SwaggerEndpoint("/swagger/v1/swagger.json", "Healthplexa API");
				options.DocumentTitle = "ASP.NET Core Event Bus API";
			});

			app.UseRouting();

			app.UseAuthorization();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
			});

			// Here we configure event handler subscriptions that the application  has to process
			ConfigureEventBusHandlers(app);
		}

		private void ConfigureEventBusDependencies(IServiceCollection services)
		{
			var rabbitMQSection = Configuration.GetSection("RabbitMQ");
			services.AddRabbitMQEventBus
			(
				connectionUrl: rabbitMQSection["ConnectionUrl"],
				brokerName: "netCoreEventBusBroker",
				queueName: "netCoreEventBusQueue",
				timeoutBeforeReconnecting: 15
			);

			services.AddTransient<MessageSentEventHandler>();
		}

		private void ConfigureEventBusHandlers(IApplicationBuilder app)
		{
			var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();

			// Here you add the event handlers for each intergration event.
			eventBus.Subscribe<MessageSentEvent, MessageSentEventHandler>();
		}
	}
}