using Elastic.Apm.AspNetCore;
using Examples.ElasticSearch.Producer.Models;
using HealthChecks.UI.Client;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using System;
using System.Reflection;

namespace Examples.ElasticSearch.Producer
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

			Log.Logger = new LoggerConfiguration()
				.Enrich.FromLogContext()
				.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:30031"))
				{
					AutoRegisterTemplate = true,
					IndexFormat = $"{Assembly.GetExecutingAssembly().GetName().Name.ToLower()}-{DateTime.UtcNow:yyyy-MM}"
				})
				.CreateLogger();

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

			services.AddControllers();
			services.AddSwaggerGen();

			services.AddMassTransit(x =>
			{
				x.UsingRabbitMq((context, cfg) =>
				{
					cfg.Host(host: "localhost", h =>
				   {
					   h.Username("admin");
					   h.Password("password");
				   });

					cfg.ConfigureEndpoints(context);
				});
			});
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IOptions<Options.HealthCheckOptions> hcOptions)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}
			app.UseHttpsRedirection();

			app.UseRouting();

			app.UseAuthorization();

			app.UseSwagger();

			app.UseSwaggerUI(c =>
			{
				c.SwaggerEndpoint("/swagger/v1/swagger.json", "Producer API V1");
			});
			app.UseElasticApm(Configuration);

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
				endpoints.MapControllers();
			});
		}
	}
}
