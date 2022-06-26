using System;
using System.Collections.Generic;

namespace Examples.ElasticSearch.Producer.Options
{
	public class HealthCheckOptions
	{
		public const string SECTION_NAME = "HealthCheck";

		public string ApiPathReadiness { get; set; }
		public string ApiPathLiveness { get; set; }
		public string UiPath { get; set; }

		public IList<Uri> ExternalServiceUris { get; set; }
	}
}