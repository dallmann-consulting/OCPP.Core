﻿/*
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

        /// <summary>This indicates the success or failure of the data transfer.
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public enum DataTransferStatusEnumType
        {
            [System.Runtime.Serialization.EnumMember(Value = @"Accepted")]
            Accepted = 0,

            [System.Runtime.Serialization.EnumMember(Value = @"Rejected")]
            Rejected = 1,

            [System.Runtime.Serialization.EnumMember(Value = @"UnknownMessageId")]
            UnknownMessageId = 2,

            [System.Runtime.Serialization.EnumMember(Value = @"UnknownVendorId")]
            UnknownVendorId = 3
        }


        [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
        public partial class DataTransferResponse
    {
            [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public CustomDataType? CustomData { get; set; }

            [Newtonsoft.Json.JsonProperty("status", Required = Newtonsoft.Json.Required.Always)]
            [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public DataTransferStatusEnumType Status { get; set; }

            [Newtonsoft.Json.JsonProperty("statusInfo", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public StatusInfoType? StatusInfo { get; set; }

            /// <summary>Data without specified length or format, in response to request.
            /// </summary>
            [Newtonsoft.Json.JsonProperty("data", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public object? Data { get; set; }
        }
    }