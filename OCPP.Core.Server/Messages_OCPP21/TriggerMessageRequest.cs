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

namespace OCPP.Core.Server.Messages_OCPP21
{
#pragma warning disable // Disable all warnings

    /// <summary>Type of message to be triggered.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum MessageTriggerEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"BootNotification")]
        BootNotification = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"LogStatusNotification")]
        LogStatusNotification = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"FirmwareStatusNotification")]
        FirmwareStatusNotification = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"Heartbeat")]
        Heartbeat = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"MeterValues")]
        MeterValues = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"SignChargingStationCertificate")]
        SignChargingStationCertificate = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"SignV2GCertificate")]
        SignV2GCertificate = 6,

        [System.Runtime.Serialization.EnumMember(Value = @"SignV2G20Certificate")]
        SignV2G20Certificate = 7,

        [System.Runtime.Serialization.EnumMember(Value = @"StatusNotification")]
        StatusNotification = 8,

        [System.Runtime.Serialization.EnumMember(Value = @"TransactionEvent")]
        TransactionEvent = 9,

        [System.Runtime.Serialization.EnumMember(Value = @"SignCombinedCertificate")]
        SignCombinedCertificate = 10,

        [System.Runtime.Serialization.EnumMember(Value = @"PublishFirmwareStatusNotification")]
        PublishFirmwareStatusNotification = 11,

        [System.Runtime.Serialization.EnumMember(Value = @"CustomTrigger")]
        CustomTrigger = 12,
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class TriggerMessageRequest
    {
        [Newtonsoft.Json.JsonProperty("evse", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public EVSEType? Evse { get; set; }

        [Newtonsoft.Json.JsonProperty("requestedMessage", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public MessageTriggerEnumType RequestedMessage { get; set; }

        /// <summary>*(2.1)* When _requestedMessage_ = `CustomTrigger` this will trigger sending the corresponding message in field _customTrigger_, if supported by Charging Station.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("customTrigger", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string? CustomTrigger { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }
}
