namespace Examples.ElasticSearch.Consumer
{
	using Examples.ElasticSearch.Consumer.Consumers;
	using MassTransit;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Hosting;
	using System;
	using Microsoft.Extensions.Configuration;
	using Microsoft.AspNetCore.Hosting;

	public class Program
	{
		public static void Main(string[] args)
		{
			CreateHostBuilder(args).Build().Run();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder.UseStartup<Startup>();
				});
	}
}