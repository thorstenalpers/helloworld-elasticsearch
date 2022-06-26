using Examples.ElasticSearch.Consumer.Consumers;
using Examples.ElasticSearch.Consumer.Models;
using HealthChecks.UI.Client;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Examples.ElasticSearch.Consumer
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
			var hcOptions = new Options.HealthCheckOptions();
			Configuration.GetSection(Options.HealthCheckOptions.SECTION_NAME).Bind(hcOptions);

			services.Configure<Options.HealthCheckOptions>(Configuration.GetSection(Options.HealthCheckOptions.SECTION_NAME));

			var hcBuilder = services.AddHealthChecks();

			string rabbitConnectionString = $"amqp://admin:password@localhost";
			hcBuilder.AddRabbitMQ(rabbitConnectionString: rabbitConnectionString, name: "RabbitMqCheck", tags: new[] { nameof(EHealthCheckType.READINESS) });

			services.AddHealthChecksUI(setupSettings: setup =>
			{
				setup.AddHealthCheckEndpoint("Readiness", hcOptions.ApiPathReadiness);
				setup.AddHealthCheckEndpoint("Liveness", hcOptions.ApiPathLiveness);
			}).AddInMemoryStorage();

			services.AddMassTransit(cfg =>
			{
				cfg.AddConsumer<SomeEventReceivedHandler>()
					.Endpoint(e =>
					{

								// specify the endpoint as temporary (may be non-durable, auto-delete, etc.)
								e.Temporary = false;

								// specify an optional concurrent message limit for the consumer
								e.ConcurrentMessageLimit = 8;

								// only use if needed, a sensible default is provided, and a reasonable
								// value is automatically calculated based upon ConcurrentMessageLimit if 
								// the transport supports it.
								e.PrefetchCount = 16;
					}
					);
				cfg.AddBus(provider =>
				{
					return Bus.Factory.CreateUsingRabbitMq(cfg =>
					{
						cfg.Host(host: "localhost", h =>
						{
							h.Username("admin");
							h.Password("password");
						});
						cfg.ReceiveEndpoint("EventReceived", e =>
						{
							e.Consumer<SomeEventReceivedHandler>(provider);
						});
					});

				});
			});

			services.AddHostedService<MassTransitHostedService>();

			services.AddSingleton<SomeEventReceivedHandler>();
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IOptions<Options.HealthCheckOptions> hcOptions)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseHttpsRedirection();

			app.UseRouting();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapHealthChecks(hcOptions.Value.ApiPathReadiness, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
				{
					AllowCachingResponses = false,
					Predicate = (check) => check.Tags.Contains(nameof(EHealthCheckType.READINESS)),
					ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
				});
				endpoints.MapHealthChecks(hcOptions.Value.ApiPathLiveness, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
				{
					AllowCachingResponses = false,
					Predicate = (check) => check.Tags.Contains(nameof(EHealthCheckType.READINESS)) || check.Tags.Contains(nameof(EHealthCheckType.LIVENESS)),
					ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
				});
				endpoints.MapHealthChecksUI(setup =>
				{
					setup.UIPath = hcOptions.Value.UiPath;
				});
			});
		}
	}
}
