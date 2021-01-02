/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Server.Messages_OCPP20
{
#pragma warning disable // Disable all warnings

    /// <summary>ID_ Token. Status. Authorization_ Status
    /// urn:x-oca:ocpp:uid:1:569372
    /// Current status of the ID Token.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum AuthorizationStatusEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Accepted")]
        Accepted = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Blocked")]
        Blocked = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"ConcurrentTx")]
        ConcurrentTx = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"Expired")]
        Expired = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"Invalid")]
        Invalid = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"NoCredit")]
        NoCredit = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"NotAllowedTypeEVSE")]
        NotAllowedTypeEVSE = 6,

        [System.Runtime.Serialization.EnumMember(Value = @"NotAtThisLocation")]
        NotAtThisLocation = 7,

        [System.Runtime.Serialization.EnumMember(Value = @"NotAtThisTime")]
        NotAtThisTime = 8,

        [System.Runtime.Serialization.EnumMember(Value = @"Unknown")]
        Unknown = 9,

    }

    /// <summary>Certificate status information. 
    /// - if all certificates are valid: return 'Accepted'.
    /// - if one of the certificates was revoked, return 'CertificateRevoked'.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum AuthorizeCertificateStatusEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Accepted")]
        Accepted = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"SignatureError")]
        SignatureError = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"CertificateExpired")]
        CertificateExpired = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"CertificateRevoked")]
        CertificateRevoked = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"NoCertificateAvailable")]
        NoCertificateAvailable = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"CertChainError")]
        CertChainError = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"ContractCancelled")]
        ContractCancelled = 6,

    }

    /// <summary>Enumeration of possible idToken types.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum IdTokenEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Central")]
        Central = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"eMAID")]
        EMAID = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"ISO14443")]
        ISO14443 = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"ISO15693")]
        ISO15693 = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"KeyCode")]
        KeyCode = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"Local")]
        Local = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"MacAddress")]
        MacAddress = 6,

        [System.Runtime.Serialization.EnumMember(Value = @"NoAuthorization")]
        NoAuthorization = 7,

    }

    /// <summary>Message_ Content. Format. Message_ Format_ Code
    /// urn:x-enexis:ecdm:uid:1:570848
    /// Format of the message.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum MessageFormatEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"ASCII")]
        ASCII = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"HTML")]
        HTML = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"URI")]
        URI = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"UTF8")]
        UTF8 = 3,

    }

    /// <summary>Contains a case insensitive identifier to use for the authorization and the type of authorization to support multiple forms of identifiers.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class AdditionalInfoType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        /// <summary>This field specifies the additional IdToken.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("additionalIdToken", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(36)]
        public string AdditionalIdToken { get; set; }

        /// <summary>This defines the type of the additionalIdToken. This is a custom type, so the implementation needs to be agreed upon by all involved parties.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("type", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string Type { get; set; }


    }

    /// <summary>ID_ Token
    /// urn:x-oca:ocpp:uid:2:233247
    /// Contains status information about an identifier.
    /// It is advised to not stop charging for a token that expires during charging, as ExpiryDate is only used for caching purposes. If ExpiryDate is not given, the status has no end date.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class IdTokenInfoType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("status", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public AuthorizationStatusEnumType Status { get; set; }

        /// <summary>ID_ Token. Expiry. Date_ Time
        /// urn:x-oca:ocpp:uid:1:569373
        /// Date and Time after which the token must be considered invalid.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("cacheExpiryDateTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public System.DateTimeOffset CacheExpiryDateTime { get; set; }

        /// <summary>Priority from a business point of view. Default priority is 0, The range is from -9 to 9. Higher values indicate a higher priority. The chargingPriority in &amp;lt;&amp;lt;transactioneventresponse,TransactionEventResponse&amp;gt;&amp;gt; overrules this one. 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("chargingPriority", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int ChargingPriority { get; set; }

        /// <summary>ID_ Token. Language1. Language_ Code
        /// urn:x-oca:ocpp:uid:1:569374
        /// Preferred user interface language of identifier user. Contains a language code as defined in &amp;lt;&amp;lt;ref-RFC5646,[RFC5646]&amp;gt;&amp;gt;.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("language1", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(8)]
        public string Language1 { get; set; }

        /// <summary>Only used when the IdToken is only valid for one or more specific EVSEs, not for the entire Charging Station.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("evseId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.ICollection<int> EvseId { get; set; }

        [Newtonsoft.Json.JsonProperty("groupIdToken", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public IdTokenType GroupIdToken { get; set; }

        /// <summary>ID_ Token. Language2. Language_ Code
        /// urn:x-oca:ocpp:uid:1:569375
        /// Second preferred user interface language of identifier user. Don’t use when language1 is omitted, has to be different from language1. Contains a language code as defined in &amp;lt;&amp;lt;ref-RFC5646,[RFC5646]&amp;gt;&amp;gt;.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("language2", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(8)]
        public string Language2 { get; set; }

        [Newtonsoft.Json.JsonProperty("personalMessage", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public MessageContentType PersonalMessage { get; set; }


    }

    /// <summary>Contains a case insensitive identifier to use for the authorization and the type of authorization to support multiple forms of identifiers.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class IdTokenType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("additionalInfo", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.ICollection<AdditionalInfoType> AdditionalInfo { get; set; }

        /// <summary>IdToken is case insensitive. Might hold the hidden id of an RFID tag, but can for example also contain a UUID.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("idToken", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(36)]
        public string IdToken { get; set; }

        [Newtonsoft.Json.JsonProperty("type", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public IdTokenEnumType Type { get; set; }


    }

    /// <summary>Message_ Content
    /// urn:x-enexis:ecdm:uid:2:234490
    /// Contains message details, for a message to be displayed on a Charging Station.
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class MessageContentType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("format", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public MessageFormatEnumType Format { get; set; }

        /// <summary>Message_ Content. Language. Language_ Code
        /// urn:x-enexis:ecdm:uid:1:570849
        /// Message language identifier. Contains a language code as defined in &amp;lt;&amp;lt;ref-RFC5646,[RFC5646]&amp;gt;&amp;gt;.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("language", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(8)]
        public string Language { get; set; }

        /// <summary>Message_ Content. Content. Message
        /// urn:x-enexis:ecdm:uid:1:570852
        /// Message contents.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("content", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(512)]
        public string Content { get; set; }


    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class AuthorizeResponse
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("idTokenInfo", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public IdTokenInfoType IdTokenInfo { get; set; } = new IdTokenInfoType();

        [Newtonsoft.Json.JsonProperty("certificateStatus", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public AuthorizeCertificateStatusEnumType CertificateStatus { get; set; }


    }
}