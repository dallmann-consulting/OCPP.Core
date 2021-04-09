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

    /// <summary>Sampled_ Value. Location. Location_ Code
    /// urn:x-oca:ocpp:uid:1:569265
    /// Indicates where the measured value has been sampled. Default =  "Outlet"
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum LocationEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Body")]
        Body = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Cable")]
        Cable = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"EV")]
        EV = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"Inlet")]
        Inlet = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"Outlet")]
        Outlet = 4,

    }

    /// <summary>Sampled_ Value. Measurand. Measurand_ Code
    /// urn:x-oca:ocpp:uid:1:569263
    /// Type of measurement. Default = "Energy.Active.Import.Register"
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum MeasurandEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"")]
        Missing,

        [System.Runtime.Serialization.EnumMember(Value = @"Current.Export")]
        Current_Export,

        [System.Runtime.Serialization.EnumMember(Value = @"Current.Import")]
        Current_Import,

        [System.Runtime.Serialization.EnumMember(Value = @"Current.Offered")]
        Current_Offered,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Active.Export.Register")]
        Energy_Active_Export_Register,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Active.Import.Register")]
        Energy_Active_Import_Register,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Reactive.Export.Register")]
        Energy_Reactive_Export_Register,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Reactive.Import.Register")]
        Energy_Reactive_Import_Register,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Active.Export.Interval")]
        Energy_Active_Export_Interval,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Active.Import.Interval")]
        Energy_Active_Import_Interval,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Active.Net")]
        Energy_Active_Net,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Reactive.Export.Interval")]
        Energy_Reactive_Export_Interval,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Reactive.Import.Interval")]
        Energy_Reactive_Import_Interval,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Reactive.Net")]
        Energy_Reactive_Net,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Apparent.Net")]
        Energy_Apparent_Net,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Apparent.Import")]
        Energy_Apparent_Import,

        [System.Runtime.Serialization.EnumMember(Value = @"Energy.Apparent.Export")]
        Energy_Apparent_Export,

        [System.Runtime.Serialization.EnumMember(Value = @"Frequency")]
        Frequency,

        [System.Runtime.Serialization.EnumMember(Value = @"Power.Active.Export")]
        Power_Active_Export,

        [System.Runtime.Serialization.EnumMember(Value = @"Power.Active.Import")]
        Power_Active_Import,

        [System.Runtime.Serialization.EnumMember(Value = @"Power.Factor")]
        Power_Factor,

        [System.Runtime.Serialization.EnumMember(Value = @"Power.Offered")]
        Power_Offered,

        [System.Runtime.Serialization.EnumMember(Value = @"Power.Reactive.Export")]
        Power_Reactive_Export,

        [System.Runtime.Serialization.EnumMember(Value = @"Power.Reactive.Import")]
        Power_Reactive_Import,

        [System.Runtime.Serialization.EnumMember(Value = @"SoC")]
        SoC,

        [System.Runtime.Serialization.EnumMember(Value = @"Voltage")]
        Voltage

    }

    /// <summary>Sampled_ Value. Phase. Phase_ Code
    /// urn:x-oca:ocpp:uid:1:569264
    /// Indicates how the measured value is to be interpreted. For instance between L1 and neutral (L1-N) Please note that not all values of phase are applicable to all Measurands. When phase is absent, the measured value is interpreted as an overall value.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum PhaseEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"L1")]
        L1 = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"L2")]
        L2 = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"L3")]
        L3 = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"N")]
        N = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"L1-N")]
        L1N = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"L2-N")]
        L2N = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"L3-N")]
        L3N = 6,

        [System.Runtime.Serialization.EnumMember(Value = @"L1-L2")]
        L1L2 = 7,

        [System.Runtime.Serialization.EnumMember(Value = @"L2-L3")]
        L2L3 = 8,

        [System.Runtime.Serialization.EnumMember(Value = @"L3-L1")]
        L3L1 = 9,

    }

    /// <summary>Sampled_ Value. Context. Reading_ Context_ Code
    /// urn:x-oca:ocpp:uid:1:569261
    /// Type of detail value: start, end or sample. Default = "Sample.Periodic"
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum ReadingContextEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Interruption.Begin")]
        Interruption_Begin = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Interruption.End")]
        Interruption_End = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"Other")]
        Other = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"Sample.Clock")]
        Sample_Clock = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"Sample.Periodic")]
        Sample_Periodic = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"Transaction.Begin")]
        Transaction_Begin = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"Transaction.End")]
        Transaction_End = 6,

        [System.Runtime.Serialization.EnumMember(Value = @"Trigger")]
        Trigger = 7,

    }

    /// <summary>Meter_ Value
    /// urn:x-oca:ocpp:uid:2:233265
    /// Collection of one or more sampled values in MeterValuesRequest and TransactionEvent. All sampled values in a MeterValue are sampled at the same point in time.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class MeterValueType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("sampledValue", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.ICollection<SampledValueType> SampledValue { get; set; } = new System.Collections.ObjectModel.Collection<SampledValueType>();

        /// <summary>Meter_ Value. Timestamp. Date_ Time
        /// urn:x-oca:ocpp:uid:1:569259
        /// Timestamp for measured value(s).
        /// </summary>
        [Newtonsoft.Json.JsonProperty("timestamp", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset Timestamp { get; set; }


    }

    /// <summary>Sampled_ Value
    /// urn:x-oca:ocpp:uid:2:233266
    /// Single sampled value in MeterValues. Each value can be accompanied by optional fields.
    /// 
    /// To save on mobile data usage, default values of all of the optional fields are such that. The value without any additional fields will be interpreted, as a register reading of active import energy in Wh (Watt-hour) units.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class SampledValueType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        /// <summary>Sampled_ Value. Value. Measure
        /// urn:x-oca:ocpp:uid:1:569260
        /// Indicates the measured value.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("value", Required = Newtonsoft.Json.Required.Always)]
        public double Value { get; set; }

        [Newtonsoft.Json.JsonProperty("context", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public ReadingContextEnumType Context { get; set; }

        [Newtonsoft.Json.JsonProperty("measurand", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public MeasurandEnumType Measurand { get; set; }

        [Newtonsoft.Json.JsonProperty("phase", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public PhaseEnumType Phase { get; set; }

        [Newtonsoft.Json.JsonProperty("location", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public LocationEnumType Location { get; set; }

        [Newtonsoft.Json.JsonProperty("signedMeterValue", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public SignedMeterValueType SignedMeterValue { get; set; }

        [Newtonsoft.Json.JsonProperty("unitOfMeasure", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public UnitOfMeasureType UnitOfMeasure { get; set; }


    }

    /// <summary>Represent a signed version of the meter value.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class SignedMeterValueType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        /// <summary>Base64 encoded, contains the signed data which might contain more then just the meter value. It can contain information like timestamps, reference to a customer etc.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("signedMeterData", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(2500)]
        public string SignedMeterData { get; set; }

        /// <summary>Method used to create the digital signature.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("signingMethod", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string SigningMethod { get; set; }

        /// <summary>Method used to encode the meter values before applying the digital signature algorithm.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("encodingMethod", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string EncodingMethod { get; set; }

        /// <summary>Base64 encoded, sending depends on configuration variable _PublicKeyWithSignedMeterValue_.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("publicKey", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(2500)]
        public string PublicKey { get; set; }


    }

    /// <summary>Represents a UnitOfMeasure with a multiplier
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class UnitOfMeasureType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        /// <summary>Unit of the value. Default = "Wh" if the (default) measurand is an "Energy" type.
        /// This field SHALL use a value from the list Standardized Units of Measurements in Part 2 Appendices. 
        /// If an applicable unit is available in that list, otherwise a "custom" unit might be used.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("unit", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(20)]
        public string Unit { get; set; } = "Wh";

        /// <summary>Multiplier, this value represents the exponent to base 10. I.e. multiplier 3 means 10 raised to the 3rd power. Default is 0.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("multiplier", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int Multiplier { get; set; } = 0;


    }

    /// <summary>Request_ Body
    /// urn:x-enexis:ecdm:uid:2:234744
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class MeterValuesRequest
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        /// <summary>Request_ Body. EVSEID. Numeric_ Identifier
        /// urn:x-enexis:ecdm:uid:1:571101
        /// This contains a number (&amp;gt;0) designating an EVSE of the Charging Station. ‘0’ (zero) is used to designate the main power meter.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("evseId", Required = Newtonsoft.Json.Required.Always)]
        public int EvseId { get; set; }

        [Newtonsoft.Json.JsonProperty("meterValue", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.ICollection<MeterValueType> MeterValue { get; set; } = new System.Collections.ObjectModel.Collection<MeterValueType>();
    }
}