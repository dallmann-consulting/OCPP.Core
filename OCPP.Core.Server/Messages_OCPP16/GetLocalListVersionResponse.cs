namespace OCPP.Core.Server.Messages_OCPP16
{
    /// <summary>
    /// This contains the field definition of the <see langword="GetLocalListVersion.conf"/> PDU 
    /// sent by the Charge Point to Central System 
    /// in response to a <see langword="GetLocalListVersion.req"/> 
    /// (i.e. <see cref="GetLocalListVersionRequest"/>) PDU.
    /// </summary>
    public class GetLocalListVersionResponse
    {
        /// <summary>
        /// Required. <br/>
        /// This contains the current version number of the local authorization list in the Charge Point.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("listVersion", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Int32 ListVersion { get; set; }
    }
}
