using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace OCPP.Core.Server.Messages_OCPP16
{
    #region SendLocalListResponse Specific Members
    /// <summary>
    /// Type of update for a <see langword="SendLocalList.req"/> 
    /// (i.e. <see cref="SendLocalListRequest"/>).
    /// </summary>
    public enum UpdateStatus
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
        /// Update of Local Authorization List is not supported by Charge Point.
        /// </summary>
        [EnumMember(Value = @"NotSupported")]
        NotSupported,

        /// <summary>
        /// Version number in the request for a differential update is less or equal then version number of current list
        /// </summary>
        [EnumMember(Value = @"VersionMismatch")]
        VersionMismatch
    }
    #endregion

    /// <summary>
    /// This contains the field definition of the <see langword="SendLocalList.conf"/> PDU 
    /// sent by the Charge Point to the Central System 
    /// in response to a <see langword="SendLocalList.req"/> PDU (i.e. <see cref="SendLocalListRequest"/>).
    /// </summary>
    public class SendLocalListResponse
    {
        /// <summary>
        /// Required. <br/>
        /// This indicates whether the Charge Point has successfully received and applied the update of the local authorization list.
        /// </summary>
        [JsonProperty("status", Required = Required.Always)]
        [Required(AllowEmptyStrings = true)]
        public UpdateStatus Status { get; set; }
    }
}
