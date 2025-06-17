using Newtonsoft.Json;
using System.Collections.Generic;

namespace OCPP.Core.Server.Messages_OCPP16
{
	public class GetConfigurationResponse
	{
		[JsonProperty("configurationKey")]
		public ICollection<OcppKeyValue> ConfigurationKey { get; set; }
		[JsonProperty("unknownKey")]
		public ICollection<string> UnknownKey { get; set; }
	}

	public class OcppKeyValue
	{
		public string Key { get; set; }
		public bool ReadOnly { get; set; }
		public string Value { get; set; }
	}
}
