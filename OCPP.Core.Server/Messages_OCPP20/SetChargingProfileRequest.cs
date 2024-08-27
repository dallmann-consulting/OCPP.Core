/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2024 dallmann consulting GmbH.
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

        /// <summary>Charging_ Profile. Charging_ Profile_ Kind. Charging_ Profile_ Kind_ Code
        /// urn:x-oca:ocpp:uid:1:569232
        /// Indicates the kind of schedule.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public enum ChargingProfileKindEnumType
        {
            [System.Runtime.Serialization.EnumMember(Value = @"Absolute")]
            Absolute = 0,

            [System.Runtime.Serialization.EnumMember(Value = @"Recurring")]
            Recurring = 1,

            [System.Runtime.Serialization.EnumMember(Value = @"Relative")]
            Relative = 2,

        }

        /// <summary>Charging_ Profile. Recurrency_ Kind. Recurrency_ Kind_ Code
        /// urn:x-oca:ocpp:uid:1:569233
        /// Indicates the start point of a recurrence.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public enum RecurrencyKindEnumType
        {
            [System.Runtime.Serialization.EnumMember(Value = @"Daily")]
            Daily = 0,

            [System.Runtime.Serialization.EnumMember(Value = @"Weekly")]
            Weekly = 1,
        }

        /// <summary>Charging_ Profile
        /// urn:x-oca:ocpp:uid:2:233255
        /// A ChargingProfile consists of ChargingSchedule, describing the amount of power or current that can be delivered per time interval.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class ChargingProfileType
        {
            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType CustomData { get; set; }

            /// <summary>Identified_ Object. MRID. Numeric_ Identifier
            /// urn:x-enexis:ecdm:uid:1:569198
            /// Id of ChargingProfile.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.Always)]
            public int Id { get; set; }

            /// <summary>Charging_ Profile. Stack_ Level. Counter
            /// urn:x-oca:ocpp:uid:1:569230
            /// Value determining level in hierarchy stack of profiles. Higher values have precedence over lower values. Lowest level is 0.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("stackLevel", Required = Newtonsoft.Json.Required.Always)]
            public int StackLevel { get; set; }

            [Newtonsoft.Json.JsonProperty("chargingProfilePurpose", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public ChargingProfilePurposeEnumType ChargingProfilePurpose { get; set; }

            [Newtonsoft.Json.JsonProperty("chargingProfileKind", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public ChargingProfileKindEnumType ChargingProfileKind { get; set; }

            [Newtonsoft.Json.JsonProperty("recurrencyKind", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public RecurrencyKindEnumType RecurrencyKind { get; set; }

            /// <summary>Charging_ Profile. Valid_ From. Date_ Time
            /// urn:x-oca:ocpp:uid:1:569234
            /// Point in time at which the profile starts to be valid. If absent, the profile is valid as soon as it is received by the Charging Station.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("validFrom", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public System.DateTimeOffset ValidFrom { get; set; }

            /// <summary>Charging_ Profile. Valid_ To. Date_ Time
            /// urn:x-oca:ocpp:uid:1:569235
            /// Point in time at which the profile stops to be valid. If absent, the profile is valid until it is replaced by another profile.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("validTo", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public System.DateTimeOffset ValidTo { get; set; }

            [Newtonsoft.Json.JsonProperty("chargingSchedule", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required]
            [System.ComponentModel.DataAnnotations.MinLength(1)]
            [System.ComponentModel.DataAnnotations.MaxLength(3)]
            public System.Collections.Generic.ICollection<ChargingScheduleType> ChargingSchedule { get; set; } = new System.Collections.ObjectModel.Collection<ChargingScheduleType>();

            /// <summary>SHALL only be included if ChargingProfilePurpose is set to TxProfile. The transactionId is used to match the profile to a specific transaction.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("transactionId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [System.ComponentModel.DataAnnotations.StringLength(36)]
            public string TransactionId { get; set; }
        }


        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class SetChargingProfileRequest
        {
            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType CustomData { get; set; }

            /// <summary>For TxDefaultProfile an evseId=0 applies the profile to each individual evse. For ChargingStationMaxProfile and ChargingStationExternalConstraints an evseId=0 contains an overal limit for the whole Charging Station.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("evseId", Required = Newtonsoft.Json.Required.Always)]
            public int EvseId { get; set; }

            [Newtonsoft.Json.JsonProperty("chargingProfile", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required]
            public ChargingProfileType ChargingProfile { get; set; } = new ChargingProfileType();
        }
    }