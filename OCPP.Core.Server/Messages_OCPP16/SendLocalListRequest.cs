using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace OCPP.Core.Server.Messages_OCPP16
{
    #region SendLocalListRequest Specific Members
    /// <summary>
    /// Type of update for a <see langword="SendLocalList.req"/> (i.e. <see cref="SendLocalListRequest"/>).
    /// </summary>
    public enum UpdateType
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

    /// <summary>
    /// Elements that constitute an entry of a Local Authorization List update.
    /// </summary>
    public class AuthorizationData
    {
        /// <summary>
        /// Required. <br/>
        /// The identifier to which this authorization applies.
        /// </summary>
        [JsonProperty("idTag", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
        [StringLength(20)]
        public string IdTag { get; set; }

        [JsonProperty("idTagInfo", Required = Required.Default)]
        public IdTagInfo IdTagInfo { get; set; }
    }
    #endregion

    /// <summary>
    /// This contains the field definition of the <see langword="SendLocalList.req"/> PDU 
    /// sent by the Central System to the Charge Point.
    /// </summary>
    /// <remarks>
    /// If no (empty) <see langword="localAuthorizationList"/> is given 
    /// and the <see langword="updateType"/> is Full, all identifications are removed from the list.
    /// Requesting a Differential update without (empty) <see langword="localAuthorizationList"/> will have no effect on the list. 
    /// All idTags in the <see langword="localAuthorizationList"/> <strong>MUST</strong> be unique, no duplicate values are
    /// allowed.
    /// </remarks>
    public class SendLocalListRequest
    {
        /// <summary>
        /// Required. <br/>
        /// In case of a full update this is the version number of the full list. 
        /// In case of a differential update it is the version number of the list after the update has been applied.
        /// </summary>
        [JsonProperty("listVersion", Required = Required.Always)]
        [Required(AllowEmptyStrings = true)]
        public int ListVersion { get; set; }

        /// <summary>
        /// Optional. <br/>
        /// In case of a full update this contains the list of values that form the new local authorization list.
        /// In case of a differential update it contains the changes to be applied to the local authorization list in the Charge Point.
        /// Maximum number of AuthorizationData elements is available in the configuration key: SendLocalListMaxLength.
        /// </summary>
        [JsonProperty("localAuthorizationList", Required = Required.Always)]
        [Required(AllowEmptyStrings = true)]
        public List<AuthorizationData> LocalAuthorizationList { get; set; }

        /// <summary>
        /// Required. <br/>
        /// This contains the type of update (full or differential) of this request.
        /// </summary>
        [JsonProperty("updateType", Required = Required.Always)]
        [Required(AllowEmptyStrings = true)]
        [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public UpdateType Type { get; set; }
    }
}
