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

        /// <summary>Source of the charging limit.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public enum ChargingLimitSourceEnumType
        {
            [System.Runtime.Serialization.EnumMember(Value = @"EMS")]
            EMS = 0,

            [System.Runtime.Serialization.EnumMember(Value = @"Other")]
            Other = 1,

            [System.Runtime.Serialization.EnumMember(Value = @"SO")]
            SO = 2,

            [System.Runtime.Serialization.EnumMember(Value = @"CSO")]
            CSO = 3,

        }

        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class ClearedChargingLimitRequest
    {
            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType CustomData { get; set; }

            [Newtonsoft.Json.JsonProperty("chargingLimitSource", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public ChargingLimitSourceEnumType ChargingLimitSource { get; set; }

            /// <summary>EVSE Identifier.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("evseId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public int EvseId { get; set; }
        }
    }