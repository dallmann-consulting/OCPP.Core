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

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class SetVariableDataType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("attributeType", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public AttributeEnumType? AttributeType { get; set; }

        /// <summary>Value to be assigned to attribute of variable.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("attributeValue", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(1000)]
        public string AttributeValue { get; set; }

        [Newtonsoft.Json.JsonProperty("component", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public ComponentType Component { get; set; } = new ComponentType();

        [Newtonsoft.Json.JsonProperty("variable", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public VariableType Variable { get; set; } = new VariableType();
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class SetVariablesRequest
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("setVariableData", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.List<SetVariableDataType> SetVariableData { get; set; } = new System.Collections.Generic.List<SetVariableDataType>();
    }
}
