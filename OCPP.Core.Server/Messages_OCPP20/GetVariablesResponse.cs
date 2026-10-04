/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2026 dallmann consulting GmbH.
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

    /// <summary>Result status of getting the variable.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum GetVariableStatusEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Accepted")]
        Accepted = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Rejected")]
        Rejected = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"UnknownComponent")]
        UnknownComponent = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"UnknownVariable")]
        UnknownVariable = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"NotSupportedAttributeType")]
        NotSupportedAttributeType = 4
    }

    /// <summary>Class to hold results of GetVariables request.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class GetVariableResultType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("attributeStatusInfo", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public StatusInfoType? AttributeStatusInfo { get; set; }

        [Newtonsoft.Json.JsonProperty("attributeStatus", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public GetVariableStatusEnumType AttributeStatus { get; set; }

        [Newtonsoft.Json.JsonProperty("attributeType", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public AttributeEnumType? AttributeType { get; set; }

        /// <summary>Value of requested attribute type of component-variable. This field can only be empty when the given status is NOT accepted.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("attributeValue", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(2500)]
        public string? AttributeValue { get; set; }

        [Newtonsoft.Json.JsonProperty("component", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public ComponentType Component { get; set; } = new ComponentType();

        [Newtonsoft.Json.JsonProperty("variable", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public VariableType Variable { get; set; } = new VariableType();
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class GetVariablesResponse
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("getVariableResult", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.List<GetVariableResultType> GetVariableResult { get; set; } = new System.Collections.Generic.List<GetVariableResultType>();
    }
}
