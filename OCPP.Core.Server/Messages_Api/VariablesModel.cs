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
using Newtonsoft.Json;

namespace OCPP.Core.Server.Messages_Api
{
    /// <summary>
    /// Protocol independent model for the API functions GetVariables/SetVariables.
    /// It follows the OCPP 2.x device model (component + variable). OCPP 1.6 is mapped
    /// as a special case: no component and the configuration key as variable name.
    /// </summary>
    public static class ApiVariableStatus
    {
        public const string Accepted = "Accepted";
        public const string Rejected = "Rejected";
        public const string UnknownComponent = "UnknownComponent";
        public const string UnknownVariable = "UnknownVariable";
        public const string NotSupportedAttributeType = "NotSupportedAttributeType";
        public const string RebootRequired = "RebootRequired";
        public const string Timeout = "Timeout";
    }

    /// <summary>
    /// Overall status of a GetVariables response for all variables (report)
    /// </summary>
    public static class ApiReportStatus
    {
        public const string Incomplete = "Incomplete";
        public const string Timeout = "Timeout";
        public const string Disconnected = "Disconnected";
        public const string TooLarge = "TooLarge";
        public const string Rejected = "Rejected";
        public const string NotSupported = "NotSupported";
    }

    public static class ApiAttributeType
    {
        public const string Actual = "Actual";
        public const string Target = "Target";
        public const string MinSet = "MinSet";
        public const string MaxSet = "MaxSet";
    }

    public static class ApiMutability
    {
        public const string ReadOnly = "ReadOnly";
        public const string WriteOnly = "WriteOnly";
        public const string ReadWrite = "ReadWrite";
    }

    public class ApiEvse
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("connectorId", NullValueHandling = NullValueHandling.Ignore)]
        public int? ConnectorId { get; set; }
    }

    public class ApiComponent
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("instance", NullValueHandling = NullValueHandling.Ignore)]
        public string Instance { get; set; }

        [JsonProperty("evse", NullValueHandling = NullValueHandling.Ignore)]
        public ApiEvse Evse { get; set; }
    }

    public class ApiVariable
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("instance", NullValueHandling = NullValueHandling.Ignore)]
        public string Instance { get; set; }
    }

    /// <summary>
    /// One entry of a GetVariables/SetVariables API request
    /// </summary>
    public class ApiVariableData
    {
        [JsonProperty("component", NullValueHandling = NullValueHandling.Ignore)]
        public ApiComponent Component { get; set; }

        [JsonProperty("variable")]
        public ApiVariable Variable { get; set; }

        /// <summary>
        /// Actual (default), Target, MinSet, MaxSet
        /// </summary>
        [JsonProperty("attributeType", NullValueHandling = NullValueHandling.Ignore)]
        public string AttributeType { get; set; }

        /// <summary>
        /// New value (only for SetVariables)
        /// </summary>
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public string Value { get; set; }

        /// <summary>
        /// True, if the entry has no (named) component => OCPP 1.6 configuration key
        /// </summary>
        [JsonIgnore]
        public bool HasComponent
        {
            get { return !string.IsNullOrWhiteSpace(Component?.Name); }
        }

        /// <summary>
        /// True, if the attribute type is empty or "Actual"
        /// </summary>
        [JsonIgnore]
        public bool IsActualAttribute
        {
            get { return string.IsNullOrEmpty(AttributeType) || string.Equals(AttributeType, ApiAttributeType.Actual, StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Creates an entry from the short URL format "[Component.]Variable"
        /// </summary>
        public static ApiVariableData FromKey(string key, string value)
        {
            ApiVariableData data = new ApiVariableData();
            data.Value = value;

            int sep = key.IndexOf('.');
            if (sep > 0)
            {
                data.Component = new ApiComponent() { Name = key.Substring(0, sep) };
                data.Variable = new ApiVariable() { Name = key.Substring(sep + 1) };
            }
            else
            {
                data.Variable = new ApiVariable() { Name = key };
            }
            return data;
        }

        /// <summary>
        /// Creates a result object for this entry with the given status
        /// </summary>
        public ApiVariableResult CreateResult(string status, string statusInfo = null)
        {
            return new ApiVariableResult()
            {
                Component = Component,
                Variable = Variable,
                AttributeType = AttributeType,
                Status = status,
                StatusInfo = statusInfo
            };
        }

        public override string ToString()
        {
            string comp = HasComponent ? $"{Component.Name}{(string.IsNullOrEmpty(Component.Instance) ? "" : "[" + Component.Instance + "]")}{(Component.Evse != null ? "@" + Component.Evse.Id : "")}." : "";
            return $"{comp}{Variable?.Name}{(string.IsNullOrEmpty(Variable?.Instance) ? "" : "[" + Variable.Instance + "]")}";
        }
    }

    /// <summary>
    /// API request for GetVariables/SetVariables
    /// </summary>
    public class ApiVariablesRequest
    {
        [JsonProperty("variables")]
        public List<ApiVariableData> Variables { get; set; } = new List<ApiVariableData>();

        /// <summary>
        /// OCPP 2.x: report base for reading all variables (ConfigurationInventory (default), FullInventory, SummaryInventory)
        /// </summary>
        [JsonProperty("reportBase", NullValueHandling = NullValueHandling.Ignore)]
        public string ReportBase { get; set; }
    }

    /// <summary>
    /// One entry of a GetVariables/SetVariables API response
    /// </summary>
    public class ApiVariableResult
    {
        [JsonProperty("component", NullValueHandling = NullValueHandling.Ignore)]
        public ApiComponent Component { get; set; }

        [JsonProperty("variable")]
        public ApiVariable Variable { get; set; }

        [JsonProperty("attributeType", NullValueHandling = NullValueHandling.Ignore)]
        public string AttributeType { get; set; }

        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public string Value { get; set; }

        /// <summary>
        /// Values of the OCPP 2.x Get-/SetVariableStatusEnumType or "Timeout"
        /// </summary>
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("statusInfo", NullValueHandling = NullValueHandling.Ignore)]
        public string StatusInfo { get; set; }

        /// <summary>
        /// ReadOnly, WriteOnly, ReadWrite (if known)
        /// </summary>
        [JsonProperty("mutability", NullValueHandling = NullValueHandling.Ignore)]
        public string Mutability { get; set; }

        /// <summary>
        /// Characteristics (only OCPP 2.x reports)
        /// </summary>
        [JsonProperty("dataType", NullValueHandling = NullValueHandling.Ignore)]
        public string DataType { get; set; }

        [JsonProperty("unit", NullValueHandling = NullValueHandling.Ignore)]
        public string Unit { get; set; }

        [JsonProperty("minLimit", NullValueHandling = NullValueHandling.Ignore)]
        public double? MinLimit { get; set; }

        [JsonProperty("maxLimit", NullValueHandling = NullValueHandling.Ignore)]
        public double? MaxLimit { get; set; }

        [JsonProperty("valuesList", NullValueHandling = NullValueHandling.Ignore)]
        public string ValuesList { get; set; }
    }

    /// <summary>
    /// API response for GetVariables/SetVariables
    /// </summary>
    public class ApiVariablesResponse
    {
        /// <summary>
        /// Only set for reports (all variables) that are not complete => see ApiReportStatus
        /// </summary>
        [JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
        public string Status { get; set; }

        [JsonProperty("statusInfo", NullValueHandling = NullValueHandling.Ignore)]
        public string StatusInfo { get; set; }

        /// <summary>
        /// Sequence numbers of missing report parts (status "Incomplete")
        /// </summary>
        [JsonProperty("missingSeqNo", NullValueHandling = NullValueHandling.Ignore)]
        public List<int> MissingSeqNo { get; set; }

        [JsonProperty("variables")]
        public List<ApiVariableResult> Variables { get; set; } = new List<ApiVariableResult>();
    }
}
