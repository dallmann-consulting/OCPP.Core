using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Server.Messages_OCPP20
{
    /// <summary>
    /// This contains the field definition of the GetLocalListVersionResponse PDU sent by the Charging Station to CSMS 
    /// in response to a <see cref="GetLocalListVersionRequest"/>.
    /// </summary>
    public class GetLocalListVersionResponse
    {
        /// <summary>
        /// Required. This contains the current version number of the local authorization list in the Charging Station.
        /// </summary>
        [JsonProperty("versionNumber", Required = Required.Always)]
        [Required(AllowEmptyStrings = false)]
        public int VersionNumber { get; set; }
    }
}
