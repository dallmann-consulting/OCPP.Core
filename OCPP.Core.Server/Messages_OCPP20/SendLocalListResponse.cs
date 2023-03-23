using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Server.Messages_OCPP20
{
    #region SendLocalListResponse Specific Members
    public enum SendLocalListStatusEnumType
    {
        /// <summary>
        /// Local Authorization List successfully updated.
        /// </summary>
        [EnumMember(Value = @"Accepted")]
        Accepted,

        /// <summary>
        /// Failed to update the Local Authorization List.
        /// </summary>
        [EnumMember(Value = @"Failed")]
        Failed,

        /// <summary>
        /// Version number in the request for a differential update is less or equal then version number of current list.
        /// </summary>
        [EnumMember(Value = @"VersionMismatch")]
        VersionMismatch
    }
    #endregion

    /// <summary>
    /// This contains the field definition of the SendLocalListResponse PDU sent by the Charging Station to the CSMS 
    /// in response to <see cref="SendLocalListRequest"/> PDU.
    /// </summary>
    public class SendLocalListResponse
    {
        /// <summary>
        /// Required. This indicates whether the Charging Station has successfully received 
        /// and applied the update of the Local Authorization List.
        /// </summary>
        [JsonProperty("status", Required = Required.Always)]
        [Required(AllowEmptyStrings = false)]
        public SendLocalListStatusEnumType Status { get; set; }

        /// <summary>
        /// Optional. Detailed status information.
        /// </summary>
        [JsonProperty("statusInfo", Required = Required.Default)]
        public StatusInfoType StatusInfo { get; set; }
    }
}
