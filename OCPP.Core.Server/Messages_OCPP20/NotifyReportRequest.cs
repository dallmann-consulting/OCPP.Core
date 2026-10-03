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

    /// <summary>Data type of this variable.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum DataEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"string")]
        @string = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"decimal")]
        @decimal = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"integer")]
        integer = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"dateTime")]
        dateTime = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"boolean")]
        boolean = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"OptionList")]
        OptionList = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"SequenceList")]
        SequenceList = 6,

        [System.Runtime.Serialization.EnumMember(Value = @"MemberList")]
        MemberList = 7
    }

    /// <summary>Defines the mutability of this attribute. Default is ReadWrite when omitted.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum MutabilityEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"ReadOnly")]
        ReadOnly = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"WriteOnly")]
        WriteOnly = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"ReadWrite")]
        ReadWrite = 2
    }

    /// <summary>Attribute data of a variable.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class VariableAttributeType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("type", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public AttributeEnumType? Type { get; set; }

        /// <summary>Value of the attribute. May only be omitted when mutability is set to 'WriteOnly'.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("value", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(2500)]
        public string? Value { get; set; }

        [Newtonsoft.Json.JsonProperty("mutability", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public MutabilityEnumType? Mutability { get; set; }

        /// <summary>If true, value will be persistent across system reboots or power down. Default when omitted is false.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("persistent", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? Persistent { get; set; }

        /// <summary>If true, value that will never be changed by the Charging Station at runtime. Default when omitted is false.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("constant", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? Constant { get; set; }
    }

    /// <summary>Fixed read-only parameters of a variable.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class VariableCharacteristicsType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        /// <summary>Unit of the variable. When the transmitted value has a unit, this field SHALL be included.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("unit", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(16)]
        public string? Unit { get; set; }

        [Newtonsoft.Json.JsonProperty("dataType", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public DataEnumType DataType { get; set; }

        /// <summary>Minimum possible value of this variable.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("minLimit", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? MinLimit { get; set; }

        /// <summary>Maximum possible value of this variable. When the datatype of this Variable is String, OptionList, SequenceList or MemberList, this field defines the maximum length of the (CSV) string.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("maxLimit", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? MaxLimit { get; set; }

        /// <summary>Allowed values when variable is Option/Member/SequenceList (comma separated).
        /// </summary>
        [Newtonsoft.Json.JsonProperty("valuesList", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(1000)]
        public string? ValuesList { get; set; }

        /// <summary>Flag indicating if this variable supports monitoring.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("supportsMonitoring", Required = Newtonsoft.Json.Required.Always)]
        public bool SupportsMonitoring { get; set; }
    }

    /// <summary>Class to report components, variables and variable attributes and characteristics.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ReportDataType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("component", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public ComponentType Component { get; set; } = new ComponentType();

        [Newtonsoft.Json.JsonProperty("variable", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public VariableType Variable { get; set; } = new VariableType();

        [Newtonsoft.Json.JsonProperty("variableAttribute", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(4)]
        public System.Collections.Generic.List<VariableAttributeType> VariableAttribute { get; set; } = new System.Collections.Generic.List<VariableAttributeType>();

        [Newtonsoft.Json.JsonProperty("variableCharacteristics", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public VariableCharacteristicsType? VariableCharacteristics { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class NotifyReportRequest
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        /// <summary>The id of the GetReportRequest  or GetBaseReportRequest that requested this report
        /// </summary>
        [Newtonsoft.Json.JsonProperty("requestId", Required = Newtonsoft.Json.Required.Always)]
        public int RequestId { get; set; }

        /// <summary>Timestamp of the moment this message was generated at the Charging Station.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("generatedAt", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset GeneratedAt { get; set; }

        [Newtonsoft.Json.JsonProperty("reportData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public System.Collections.Generic.List<ReportDataType>? ReportData { get; set; }

        /// <summary>“to be continued” indicator. Indicates whether another part of the report follows in an upcoming notifyReportRequest message. Default value when omitted is false.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("tbc", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? Tbc { get; set; }

        /// <summary>Sequence number of this message. First message starts at 0.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("seqNo", Required = Newtonsoft.Json.Required.Always)]
        public int SeqNo { get; set; }
    }
}
