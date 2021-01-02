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

namespace OCPP.Core.Server.Messages_OCPP20
{
#pragma warning disable // Disable all warnings

    /// <summary>This class does not get 'AdditionalProperties = false' in the schema generation, so it can be extended with arbitrary JSON properties to allow adding custom data.</summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class CustomDataType
    {
        [Newtonsoft.Json.JsonProperty("vendorId", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(255)]
        public string VendorId { get; set; }

        private System.Collections.Generic.IDictionary<string, object> _additionalProperties = new System.Collections.Generic.Dictionary<string, object>();

        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties; }
            set { _additionalProperties = value; }
        }


    }

    /// <summary>This contains the reason for sending this message to the CSMS.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum BootReasonEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"ApplicationReset")]
        ApplicationReset = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"FirmwareUpdate")]
        FirmwareUpdate = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"LocalReset")]
        LocalReset = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"PowerUp")]
        PowerUp = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"RemoteReset")]
        RemoteReset = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"ScheduledReset")]
        ScheduledReset = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"Triggered")]
        Triggered = 6,

        [System.Runtime.Serialization.EnumMember(Value = @"Unknown")]
        Unknown = 7,

        [System.Runtime.Serialization.EnumMember(Value = @"Watchdog")]
        Watchdog = 8,

    }

    /// <summary>Charge_ Point
    /// urn:x-oca:ocpp:uid:2:233122
    /// The physical system where an Electrical Vehicle (EV) can be charged.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ChargingStationType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        /// <summary>Device. Serial_ Number. Serial_ Number
        /// urn:x-oca:ocpp:uid:1:569324
        /// Vendor-specific device identifier.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("serialNumber", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(25)]
        public string SerialNumber { get; set; }

        /// <summary>Device. Model. CI20_ Text
        /// urn:x-oca:ocpp:uid:1:569325
        /// Defines the model of the device.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("model", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(20)]
        public string Model { get; set; }

        [Newtonsoft.Json.JsonProperty("modem", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public ModemType Modem { get; set; }

        /// <summary>Identifies the vendor (not necessarily in a unique manner).
        /// </summary>
        [Newtonsoft.Json.JsonProperty("vendorName", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string VendorName { get; set; }

        /// <summary>This contains the firmware version of the Charging Station.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("firmwareVersion", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string FirmwareVersion { get; set; }


    }

    /// <summary>Wireless_ Communication_ Module
    /// urn:x-oca:ocpp:uid:2:233306
    /// Defines parameters required for initiating and maintaining wireless communication with other devices.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ModemType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        /// <summary>Wireless_ Communication_ Module. ICCID. CI20_ Text
        /// urn:x-oca:ocpp:uid:1:569327
        /// This contains the ICCID of the modem’s SIM card.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("iccid", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(20)]
        public string Iccid { get; set; }

        /// <summary>Wireless_ Communication_ Module. IMSI. CI20_ Text
        /// urn:x-oca:ocpp:uid:1:569328
        /// This contains the IMSI of the modem’s SIM card.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("imsi", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(20)]
        public string Imsi { get; set; }


    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class BootNotificationRequest
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingStation", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public ChargingStationType ChargingStation { get; set; } = new ChargingStationType();

        [Newtonsoft.Json.JsonProperty("reason", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public BootReasonEnumType Reason { get; set; }


    }
}