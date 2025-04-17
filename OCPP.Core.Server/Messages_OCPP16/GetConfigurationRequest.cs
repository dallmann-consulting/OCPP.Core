using Newtonsoft.Json;
using System.Collections.Generic;

namespace OCPP.Core.Server.Messages_OCPP16
{
	public class GetConfigurationRequest
	{
		[JsonProperty("key")]
		public ICollection<string> Key {  get; set; }
	}
}
