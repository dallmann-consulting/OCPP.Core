using Newtonsoft.Json;
using OCPP.Core.Server.Messages_OCPP16;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace OCPP.Core.Server.Messages_OCPP20
{
    #region SendLocalListRequest Specific Members
    public enum UpdateEnumType
    {
        /// <summary>
        /// Indicates that the current Local Authorization List must be updated with the values in this message.
        /// </summary>
        [EnumMember(Value = @"Differential")]
        Differential,

        /// <summary>
        /// Indicates that the current Local Authorization List must be replaced by the values in this message.
        /// </summary>
        [EnumMember(Value = @"Full")]
        Full
    }
    #endregion

    /// <summary>
    /// This contains the field definition of the SendLocalListRequest PDU sent by the CSMS to the Charging Station.
    /// </summary>
    /// <remarks>
    /// If no (empty) localAuthorizationList is given and the updateType is Full, all IdTokens are removed from the list.
    /// Requesting a Differential update without or with empty localAuthorizationList will have no effect on the list.
    /// All IdTokens in the localAuthorizationList MUST be unique, no duplicate values are allowed.
    /// </remarks>
    public class SendLocalListRequest
    {
        /// <summary>
        /// Required. <br/>
        /// In case of a full update this is the version number of the full list.
        /// In case of a differential update it is the version number of the list after the update has been applied.
        /// </summary>
        [JsonProperty("versionNumber", Required = Required.Always)]
        [Required(AllowEmptyStrings = false)]
        public int VersionNumber { get; set; }

        /// <summary>
        /// Required. <br/>
        /// This contains the type of update (full or differential) of this request.
        /// </summary>
        [JsonProperty("updateType", Required = Required.Always)]
        [Required(AllowEmptyStrings = false)]
        public UpdateEnumType UpdateType { get; set; }

        /// <summary>
        /// Optional. <br/>
        /// This contains the Local Authorization List entries.
        /// </summary>
        [JsonProperty("localAuthorizationList", Required = Required.Default)]
        public List<AuthorizationData> LocalAuthorizationList { get; set; }
    }
}
