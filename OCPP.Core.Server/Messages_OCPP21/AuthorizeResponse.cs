/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2025 dallmann consulting GmbH.
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

namespace OCPP.Core.Server.Messages_OCPP21
{
#pragma warning disable // Disable all warnings

        /// <summary>Current status of the ID Token.
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

        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public enum DayOfWeekEnumType
        {
            [System.Runtime.Serialization.EnumMember(Value = @"Monday")]
            Monday = 0,

            [System.Runtime.Serialization.EnumMember(Value = @"Tuesday")]
            Tuesday = 1,

            [System.Runtime.Serialization.EnumMember(Value = @"Wednesday")]
            Wednesday = 2,

            [System.Runtime.Serialization.EnumMember(Value = @"Thursday")]
            Thursday = 3,

            [System.Runtime.Serialization.EnumMember(Value = @"Friday")]
            Friday = 4,

            [System.Runtime.Serialization.EnumMember(Value = @"Saturday")]
            Saturday = 5,

            [System.Runtime.Serialization.EnumMember(Value = @"Sunday")]
            Sunday = 6,

        }

        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public enum EnergyTransferModeEnumType
        {
            [System.Runtime.Serialization.EnumMember(Value = @"AC_single_phase")]
            AC_single_phase = 0,

            [System.Runtime.Serialization.EnumMember(Value = @"AC_two_phase")]
            AC_two_phase = 1,

            [System.Runtime.Serialization.EnumMember(Value = @"AC_three_phase")]
            AC_three_phase = 2,

            [System.Runtime.Serialization.EnumMember(Value = @"DC")]
            DC = 3,

            [System.Runtime.Serialization.EnumMember(Value = @"AC_BPT")]
            AC_BPT = 4,

            [System.Runtime.Serialization.EnumMember(Value = @"AC_BPT_DER")]
            AC_BPT_DER = 5,

            [System.Runtime.Serialization.EnumMember(Value = @"AC_DER")]
            AC_DER = 6,

            [System.Runtime.Serialization.EnumMember(Value = @"DC_BPT")]
            DC_BPT = 7,

            [System.Runtime.Serialization.EnumMember(Value = @"DC_ACDP")]
            DC_ACDP = 8,

            [System.Runtime.Serialization.EnumMember(Value = @"DC_ACDP_BPT")]
            DC_ACDP_BPT = 9,

            [System.Runtime.Serialization.EnumMember(Value = @"WPT")]
            WPT = 10,

        }

        /// <summary>Type of EVSE (AC, DC) this tariff applies to.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public enum EvseKindEnumType
        {
            [System.Runtime.Serialization.EnumMember(Value = @"AC")]
            AC = 0,

            [System.Runtime.Serialization.EnumMember(Value = @"DC")]
            DC = 1,

        }

        /// <summary>Format of the message.
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

            [System.Runtime.Serialization.EnumMember(Value = @"QRCODE")]
            QRCODE = 4,

        }


        /// <summary>Contains status information about an identifier.
        /// It is advised to not stop charging for a token that expires during charging, as ExpiryDate is only used for caching purposes. If ExpiryDate is not given, the status has no end date.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class IdTokenInfoType
        {
            [Newtonsoft.Json.JsonProperty("status", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public AuthorizationStatusEnumType Status { get; set; }

            /// <summary>Date and Time after which the token must be considered invalid.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("cacheExpiryDateTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public System.DateTimeOffset? CacheExpiryDateTime { get; set; }

            /// <summary>Priority from a business point of view. Default priority is 0, The range is from -9 to 9. Higher values indicate a higher priority. The chargingPriority in &amp;lt;&amp;lt;transactioneventresponse,TransactionEventResponse&amp;gt;&amp;gt; overrules this one. 
            /// </summary>
            [Newtonsoft.Json.JsonProperty("chargingPriority", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public int? ChargingPriority { get; set; }

            [Newtonsoft.Json.JsonProperty("groupIdToken", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public IdTokenType? GroupIdToken { get; set; }

            /// <summary>Preferred user interface language of identifier user. Contains a language code as defined in &amp;lt;&amp;lt;ref-RFC5646,[RFC5646]&amp;gt;&amp;gt;.
            /// 
            /// </summary>
            [Newtonsoft.Json.JsonProperty("language1", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.StringLength(8)]
            public string? Language1 { get; set; }

            /// <summary>Second preferred user interface language of identifier user. Don’t use when language1 is omitted, has to be different from language1. Contains a language code as defined in &amp;lt;&amp;lt;ref-RFC5646,[RFC5646]&amp;gt;&amp;gt;.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("language2", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.StringLength(8)]
            public string? Language2 { get; set; }

            /// <summary>Only used when the IdToken is only valid for one or more specific EVSEs, not for the entire Charging Station.
            /// 
            /// </summary>
            [Newtonsoft.Json.JsonProperty("evseId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            public System.Collections.Generic.ICollection<int>? EvseId { get; set; }

            [Newtonsoft.Json.JsonProperty("personalMessage", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public MessageContentType? PersonalMessage { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }


        /// <summary>Contains message details, for a message to be displayed on a Charging Station.
        /// 
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class MessageContentType
        {
            [Newtonsoft.Json.JsonProperty("format", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public MessageFormatEnumType Format { get; set; }

            /// <summary>Message language identifier. Contains a language code as defined in &amp;lt;&amp;lt;ref-RFC5646,[RFC5646]&amp;gt;&amp;gt;.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("language", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.StringLength(8)]
            public string? Language { get; set; }

            /// <summary>*(2.1)* Required. Message contents. +
            /// Maximum length supported by Charging Station is given in OCPPCommCtrlr.FieldLength["MessageContentType.content"].
            ///     Maximum length defaults to 1024.
            /// 
            /// </summary>
            [Newtonsoft.Json.JsonProperty("content", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
            [System.ComponentModel.DataAnnotations.StringLength(1024)]
            public string Content { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        /// <summary>Price with and without tax. At least one of _exclTax_, _inclTax_ must be present.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class PriceType
        {
            /// <summary>Price/cost excluding tax. Can be absent if _inclTax_ is present.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("exclTax", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public double? ExclTax { get; set; }

            /// <summary>Price/cost including tax. Can be absent if _exclTax_ is present.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("inclTax", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public double? InclTax { get; set; }

            [Newtonsoft.Json.JsonProperty("taxRates", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            [System.ComponentModel.DataAnnotations.MaxLength(5)]
            public System.Collections.Generic.ICollection<TaxRateType>? TaxRates { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        /// <summary>These conditions describe if a FixedPrice applies at start of the transaction.
        /// 
        /// When more than one restriction is set, they are to be treated as a logical AND. All need to be valid before this price is active.
        /// 
        /// NOTE: _startTimeOfDay_ and _endTimeOfDay_ are in local time, because it is the time in the tariff as it is shown to the EV driver at the Charging Station.
        /// A Charging Station will convert this to the internal time zone that it uses (which is recommended to be UTC, see section Generic chapter 3.1) when performing cost calculation.
        /// 
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class TariffConditionsFixedType
        {
            /// <summary>Start time of day in local time. +
            /// Format as per RFC 3339: time-hour ":" time-minute  +
            /// Must be in 24h format with leading zeros. Hour/Minute separator: ":"
            /// Regex: ([0-1][0-9]\|2[0-3]):[0-5][0-9]
            /// </summary>
            [Newtonsoft.Json.JsonProperty("startTimeOfDay", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string? StartTimeOfDay { get; set; }

            /// <summary>End time of day in local time. Same syntax as _startTimeOfDay_. +
            ///     If end time &amp;lt; start time then the period wraps around to the next day. +
            ///     To stop at end of the day use: 00:00.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("endTimeOfDay", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string? EndTimeOfDay { get; set; }

            /// <summary>Day(s) of the week this is tariff applies.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("dayOfWeek", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore, ItemConverterType = typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            [System.ComponentModel.DataAnnotations.MaxLength(7)]
            public System.Collections.Generic.ICollection<DayOfWeekEnumType>? DayOfWeek { get; set; }

            /// <summary>Start date in local time, for example: 2015-12-24.
            /// Valid from this day (inclusive). +
            /// Format as per RFC 3339: full-date  + 
            /// 
            /// Regex: ([12][0-9]{3})-(0[1-9]\|1[0-2])-(0[1-9]\|[12][0-9]\|3[01])
            /// </summary>
            [Newtonsoft.Json.JsonProperty("validFromDate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string? ValidFromDate { get; set; }

            /// <summary>End date in local time, for example: 2015-12-27.
            ///     Valid until this day (exclusive). Same syntax as _validFromDate_.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("validToDate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string? ValidToDate { get; set; }

            [Newtonsoft.Json.JsonProperty("evseKind", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public EvseKindEnumType? EvseKind { get; set; }

            /// <summary>For which payment brand this (adhoc) tariff applies. Can be used to add a surcharge for certain payment brands.
            ///     Based on value of _additionalIdToken_ from _idToken.additionalInfo.type_ = "PaymentBrand".
            /// </summary>
            [Newtonsoft.Json.JsonProperty("paymentBrand", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.StringLength(20)]
            public string? PaymentBrand { get; set; }

            /// <summary>Type of adhoc payment, e.g. CC, Debit.
            ///     Based on value of _additionalIdToken_ from _idToken.additionalInfo.type_ = "PaymentRecognition".
            /// </summary>
            [Newtonsoft.Json.JsonProperty("paymentRecognition", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.StringLength(20)]
            public string? PaymentRecognition { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        /// <summary>These conditions describe if and when a TariffEnergyType or TariffTimeType applies during a transaction.
        /// 
        /// When more than one restriction is set, they are to be treated as a logical AND. All need to be valid before this price is active.
        /// 
        /// For reverse energy flow (discharging) negative values of energy, power and current are used.
        /// 
        /// NOTE: _minXXX_ (where XXX = Kwh/A/Kw) must be read as "closest to zero", and _maxXXX_ as "furthest from zero". For example, a *charging* power range from 10 kW to 50 kWh is given by _minPower_ = 10000 and _maxPower_ = 50000, and a *discharging* power range from -10 kW to -50 kW is given by _minPower_ = -10 and _maxPower_ = -50.
        /// 
        /// NOTE: _startTimeOfDay_ and _endTimeOfDay_ are in local time, because it is the time in the tariff as it is shown to the EV driver at the Charging Station.
        /// A Charging Station will convert this to the internal time zone that it uses (which is recommended to be UTC, see section Generic chapter 3.1) when performing cost calculation.
        /// 
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class TariffConditionsType
        {
            /// <summary>Start time of day in local time. +
            /// Format as per RFC 3339: time-hour ":" time-minute  +
            /// Must be in 24h format with leading zeros. Hour/Minute separator: ":"
            /// Regex: ([0-1][0-9]\|2[0-3]):[0-5][0-9]
            /// </summary>
            [Newtonsoft.Json.JsonProperty("startTimeOfDay", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string? StartTimeOfDay { get; set; }

            /// <summary>End time of day in local time. Same syntax as _startTimeOfDay_. +
            ///     If end time &amp;lt; start time then the period wraps around to the next day. +
            ///     To stop at end of the day use: 00:00.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("endTimeOfDay", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string? EndTimeOfDay { get; set; }

            /// <summary>Day(s) of the week this is tariff applies.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("dayOfWeek", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore, ItemConverterType = typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            [System.ComponentModel.DataAnnotations.MaxLength(7)]
            public System.Collections.Generic.ICollection<DayOfWeekEnumType>? DayOfWeek { get; set; }

            /// <summary>Start date in local time, for example: 2015-12-24.
            /// Valid from this day (inclusive). +
            /// Format as per RFC 3339: full-date  + 
            /// 
            /// Regex: ([12][0-9]{3})-(0[1-9]\|1[0-2])-(0[1-9]\|[12][0-9]\|3[01])
            /// </summary>
            [Newtonsoft.Json.JsonProperty("validFromDate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string? ValidFromDate { get; set; }

            /// <summary>End date in local time, for example: 2015-12-27.
            ///     Valid until this day (exclusive). Same syntax as _validFromDate_.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("validToDate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string? ValidToDate { get; set; }

            [Newtonsoft.Json.JsonProperty("evseKind", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public EvseKindEnumType? EvseKind { get; set; }

            /// <summary>Minimum consumed energy in Wh, for example 20000 Wh.
            ///     Valid from this amount of energy (inclusive) being used.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("minEnergy", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public double? MinEnergy { get; set; }

            /// <summary>Maximum consumed energy in Wh, for example 50000 Wh.
            ///     Valid until this amount of energy (exclusive) being used.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("maxEnergy", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public double? MaxEnergy { get; set; }

            /// <summary>Sum of the minimum current (in Amperes) over all phases, for example 5 A.
            ///     When the EV is charging with more than, or equal to, the defined amount of current, this price is/becomes active. If the charging current is or becomes lower, this price is not or no longer valid and becomes inactive. +
            ///     This is NOT about the minimum current over the entire transaction.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("minCurrent", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public double? MinCurrent { get; set; }

            /// <summary>Sum of the maximum current (in Amperes) over all phases, for example 20 A.
            ///       When the EV is charging with less than the defined amount of current, this price becomes/is active. If the charging current is or becomes higher, this price is not or no longer valid and becomes inactive.
            ///       This is NOT about the maximum current over the entire transaction.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("maxCurrent", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public double? MaxCurrent { get; set; }

            /// <summary>Minimum power in W, for example 5000 W.
            ///       When the EV is charging with more than, or equal to, the defined amount of power, this price is/becomes active.
            ///       If the charging power is or becomes lower, this price is not or no longer valid and becomes inactive.
            ///       This is NOT about the minimum power over the entire transaction.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("minPower", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public double? MinPower { get; set; }

            /// <summary>Maximum power in W, for example 20000 W.
            ///       When the EV is charging with less than the defined amount of power, this price becomes/is active.
            ///       If the charging power is or becomes higher, this price is not or no longer valid and becomes inactive.
            ///       This is NOT about the maximum power over the entire transaction.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("maxPower", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public double? MaxPower { get; set; }

            /// <summary>Minimum duration in seconds the transaction (charging &amp;amp; idle) MUST last (inclusive).
            ///       When the duration of a transaction is longer than the defined value, this price is or becomes active.
            ///       Before that moment, this price is not yet active.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("minTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public int? MinTime { get; set; }

            /// <summary>Maximum duration in seconds the transaction (charging &amp;amp; idle) MUST last (exclusive).
            ///       When the duration of a transaction is shorter than the defined value, this price is or becomes active.
            ///       After that moment, this price is no longer active.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("maxTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public int? MaxTime { get; set; }

            /// <summary>Minimum duration in seconds the charging MUST last (inclusive).
            ///       When the duration of a charging is longer than the defined value, this price is or becomes active.
            ///       Before that moment, this price is not yet active.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("minChargingTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public int? MinChargingTime { get; set; }

            /// <summary>Maximum duration in seconds the charging MUST last (exclusive).
            ///       When the duration of a charging is shorter than the defined value, this price is or becomes active.
            ///       After that moment, this price is no longer active.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("maxChargingTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public int? MaxChargingTime { get; set; }

            /// <summary>Minimum duration in seconds the idle period (i.e. not charging) MUST last (inclusive).
            ///       When the duration of the idle time is longer than the defined value, this price is or becomes active.
            ///       Before that moment, this price is not yet active.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("minIdleTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public int? MinIdleTime { get; set; }

            /// <summary>Maximum duration in seconds the idle period (i.e. not charging) MUST last (exclusive).
            ///       When the duration of idle time is shorter than the defined value, this price is or becomes active.
            ///       After that moment, this price is no longer active.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("maxIdleTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public int? MaxIdleTime { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        /// <summary>Tariff with optional conditions for an energy price.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class TariffEnergyPriceType
        {
            /// <summary>Price per kWh (excl. tax) for this element.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("priceKwh", Required = Newtonsoft.Json.Required.Always)]
            public double PriceKwh { get; set; }

            [Newtonsoft.Json.JsonProperty("conditions", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public TariffConditionsType? Conditions { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        /// <summary>Price elements and tax for energy
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class TariffEnergyType
        {
            [Newtonsoft.Json.JsonProperty("prices", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            public System.Collections.Generic.ICollection<TariffEnergyPriceType> Prices { get; set; } = new System.Collections.ObjectModel.Collection<TariffEnergyPriceType>();

            [Newtonsoft.Json.JsonProperty("taxRates", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            [System.ComponentModel.DataAnnotations.MaxLength(5)]
            public System.Collections.Generic.ICollection<TaxRateType>? TaxRates { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        /// <summary>Tariff with optional conditions for a fixed price.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class TariffFixedPriceType
        {
            [Newtonsoft.Json.JsonProperty("conditions", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public TariffConditionsFixedType? Conditions { get; set; }

            /// <summary>Fixed price  for this element e.g. a start fee.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("priceFixed", Required = Newtonsoft.Json.Required.Always)]
            public double PriceFixed { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class TariffFixedType
        {
            [Newtonsoft.Json.JsonProperty("prices", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            public System.Collections.Generic.ICollection<TariffFixedPriceType> Prices { get; set; } = new System.Collections.ObjectModel.Collection<TariffFixedPriceType>();

            [Newtonsoft.Json.JsonProperty("taxRates", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            [System.ComponentModel.DataAnnotations.MaxLength(5)]
            public System.Collections.Generic.ICollection<TaxRateType>? TaxRates { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        /// <summary>Tariff with optional conditions for a time duration price.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class TariffTimePriceType
        {
            /// <summary>Price per minute (excl. tax) for this element.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("priceMinute", Required = Newtonsoft.Json.Required.Always)]
            public double PriceMinute { get; set; }

            [Newtonsoft.Json.JsonProperty("conditions", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public TariffConditionsType? Conditions { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        /// <summary>Price elements and tax for time
        /// 
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class TariffTimeType
        {
            [Newtonsoft.Json.JsonProperty("prices", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            public System.Collections.Generic.ICollection<TariffTimePriceType> Prices { get; set; } = new System.Collections.ObjectModel.Collection<TariffTimePriceType>();

            [Newtonsoft.Json.JsonProperty("taxRates", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            [System.ComponentModel.DataAnnotations.MaxLength(5)]
            public System.Collections.Generic.ICollection<TaxRateType>? TaxRates { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        /// <summary>A tariff is described by fields with prices for:
        /// energy,
        /// charging time,
        /// idle time,
        /// fixed fee,
        /// reservation time,
        /// reservation fixed fee. +
        /// Each of these fields may have (optional) conditions that specify when a price is applicable. +
        /// The _description_ contains a human-readable explanation of the tariff to be shown to the user. +
        /// The other fields are parameters that define the tariff. These are used by the charging station to calculate the price.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class TariffType
        {
            /// <summary>Unique id of tariff
            /// </summary>
            [Newtonsoft.Json.JsonProperty("tariffId", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
            [System.ComponentModel.DataAnnotations.StringLength(60)]
            public string TariffId { get; set; }

            [Newtonsoft.Json.JsonProperty("description", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            [System.ComponentModel.DataAnnotations.MaxLength(10)]
            public System.Collections.Generic.ICollection<MessageContentType>? Description { get; set; }

            /// <summary>Currency code according to ISO 4217
            /// </summary>
            [Newtonsoft.Json.JsonProperty("currency", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
            [System.ComponentModel.DataAnnotations.StringLength(3)]
            public string Currency { get; set; }

            [Newtonsoft.Json.JsonProperty("energy", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public TariffEnergyType? Energy { get; set; }

            /// <summary>Time when this tariff becomes active. When absent, it is immediately active.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("validFrom", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public System.DateTimeOffset? ValidFrom { get; set; }

            [Newtonsoft.Json.JsonProperty("chargingTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public TariffTimeType? ChargingTime { get; set; }

            [Newtonsoft.Json.JsonProperty("idleTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public TariffTimeType? IdleTime { get; set; }

            [Newtonsoft.Json.JsonProperty("fixedFee", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public TariffFixedType? FixedFee { get; set; }

            [Newtonsoft.Json.JsonProperty("reservationTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public TariffTimeType? ReservationTime { get; set; }

            [Newtonsoft.Json.JsonProperty("reservationFixed", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public TariffFixedType? ReservationFixed { get; set; }

            [Newtonsoft.Json.JsonProperty("minCost", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public PriceType? MinCost { get; set; }

            [Newtonsoft.Json.JsonProperty("maxCost", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public PriceType? MaxCost { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        /// <summary>Tax percentage
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class TaxRateType
        {
            /// <summary>Type of this tax, e.g.  "Federal ",  "State", for information on receipt.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("type", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
            [System.ComponentModel.DataAnnotations.StringLength(20)]
            public string Type { get; set; }

            /// <summary>Tax percentage
            /// </summary>
            [Newtonsoft.Json.JsonProperty("tax", Required = Newtonsoft.Json.Required.Always)]
            public double Tax { get; set; }

            /// <summary>Stack level for this type of tax. Default value, when absent, is 0. +
            /// _stack_ = 0: tax on net price; +
            /// _stack_ = 1: tax added on top of _stack_ 0; +
            /// _stack_ = 2: tax added on top of _stack_ 1, etc. 
            /// </summary>
            [Newtonsoft.Json.JsonProperty("stack", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
            public int? Stack { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }

        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class AuthorizeResponse
        {
            [Newtonsoft.Json.JsonProperty("idTokenInfo", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required]
            public IdTokenInfoType IdTokenInfo { get; set; } = new IdTokenInfoType();

            [Newtonsoft.Json.JsonProperty("certificateStatus", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public AuthorizeCertificateStatusEnumType? CertificateStatus { get; set; }

            /// <summary>*(2.1)* List of allowed energy transfer modes the EV can choose from. If omitted this defaults to charging only.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("allowedEnergyTransfer", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore, ItemConverterType = typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            public System.Collections.Generic.ICollection<EnergyTransferModeEnumType>? AllowedEnergyTransfer { get; set; }

            [Newtonsoft.Json.JsonProperty("tariff", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public TariffType? Tariff { get; set; }

            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }
        }
    }