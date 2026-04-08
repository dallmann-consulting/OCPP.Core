/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2025 dallmann consulting GmbH.
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

namespace OCPP.Core.Server.Messages_OCPP21
{
#pragma warning disable // Disable all warnings

    /// <summary>The unit of measure in which limits and setpoints are expressed.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum ChargingRateUnitEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"W")]
        W = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"A")]
        A = 1,

    }

    /// <summary>The kind of cost referred to in the message element amount
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum CostKindEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"CarbonDioxideEmission")]
        CarbonDioxideEmission = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"RelativePricePercentage")]
        RelativePricePercentage = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"RenewableGenerationPercentage")]
        RenewableGenerationPercentage = 2,

    }


    /// <summary>The AbsolutePriceScheduleType is modeled after the same type that is defined in ISO 15118-20, such that if it is supplied by an EMSP as a signed EXI message, the conversion from EXI to JSON (in OCPP) and back to EXI (for ISO 15118-20) does not change the digest and therefore does not invalidate the signature.
    /// 
    /// image::images/AbsolutePriceSchedule-Simple.png[]
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class AbsolutePriceScheduleType
    {
        /// <summary>Starting point of price schedule.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("timeAnchor", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset TimeAnchor { get; set; }

        /// <summary>Unique ID of price schedule
        /// </summary>
        [Newtonsoft.Json.JsonProperty("priceScheduleID", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int PriceScheduleID { get; set; }

        /// <summary>Description of the price schedule.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("priceScheduleDescription", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(160)]
        public string? PriceScheduleDescription { get; set; }

        /// <summary>Currency according to ISO 4217.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("currency", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(3)]
        public string Currency { get; set; }

        /// <summary>String that indicates what language is used for the human readable strings in the price schedule. Based on ISO 639.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("language", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(8)]
        public string Language { get; set; }

        /// <summary>A string in URN notation which shall uniquely identify an algorithm that defines how to compute an energy fee sum for a specific power profile based on the EnergyFee information from the PriceRule elements.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("priceAlgorithm", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(2000)]
        public string PriceAlgorithm { get; set; }

        [Newtonsoft.Json.JsonProperty("minimumCost", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public RationalNumberType? MinimumCost { get; set; }

        [Newtonsoft.Json.JsonProperty("maximumCost", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public RationalNumberType? MaximumCost { get; set; }

        [Newtonsoft.Json.JsonProperty("priceRuleStacks", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(1024)]
        public System.Collections.Generic.ICollection<PriceRuleStackType> PriceRuleStacks { get; set; } = new System.Collections.ObjectModel.Collection<PriceRuleStackType>();

        [Newtonsoft.Json.JsonProperty("taxRules", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(10)]
        public System.Collections.Generic.ICollection<TaxRuleType>? TaxRules { get; set; }

        [Newtonsoft.Json.JsonProperty("overstayRuleList", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public OverstayRuleListType? OverstayRuleList { get; set; }

        [Newtonsoft.Json.JsonProperty("additionalSelectedServices", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(5)]
        public System.Collections.Generic.ICollection<AdditionalSelectedServicesType>? AdditionalSelectedServices { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Part of ISO 15118-20 price schedule.
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class AdditionalSelectedServicesType
    {
        [Newtonsoft.Json.JsonProperty("serviceFee", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public RationalNumberType ServiceFee { get; set; } = new RationalNumberType();

        /// <summary>Human readable string to identify this service.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("serviceName", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(80)]
        public string ServiceName { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ChargingLimitType
    {
        /// <summary>Represents the source of the charging limit. Values defined in appendix as ChargingLimitSourceEnumStringType.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("chargingLimitSource", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(20)]
        public string ChargingLimitSource { get; set; }

        /// <summary>*(2.1)* True when the reported limit concerns local generation that is providing extra capacity, instead of a limitation.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("isLocalGeneration", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? IsLocalGeneration { get; set; }

        /// <summary>Indicates whether the charging limit is critical for the grid.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("isGridCritical", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? IsGridCritical { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Charging schedule period structure defines a time period in a charging schedule. It is used in: CompositeScheduleType and in ChargingScheduleType. When used in a NotifyEVChargingScheduleRequest only _startPeriod_, _limit_, _limit_L2_, _limit_L3_ are relevant.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ChargingSchedulePeriodType
    {
        /// <summary>Start of the period, in seconds from the start of schedule. The value of StartPeriod also defines the stop time of the previous period.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("startPeriod", Required = Newtonsoft.Json.Required.Always)]
        public int StartPeriod { get; set; }

        /// <summary>Optional only when not required by the _operationMode_, as in CentralSetpoint, ExternalSetpoint, ExternalLimits, LocalFrequency,  LocalLoadBalancing. +
        /// Charging rate limit during the schedule period, in the applicable _chargingRateUnit_. 
        /// This SHOULD be a non-negative value; a negative value is only supported for backwards compatibility with older systems that use a negative value to specify a discharging limit.
        /// When using _chargingRateUnit_ = `W`, this field represents the sum of the power of all phases, unless values are provided for L2 and L3, in which case this field represents phase L1.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("limit", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? Limit { get; set; }

        /// <summary>*(2.1)* Charging rate limit on phase L2  in the applicable _chargingRateUnit_. 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("limit_L2", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? Limit_L2 { get; set; }

        /// <summary>*(2.1)* Charging rate limit on phase L3  in the applicable _chargingRateUnit_. 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("limit_L3", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? Limit_L3 { get; set; }

        /// <summary>The number of phases that can be used for charging. +
        /// For a DC EVSE this field should be omitted. +
        /// For an AC EVSE a default value of _numberPhases_ = 3 will be assumed if the field is absent.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("numberPhases", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, 3)]
        public int? NumberPhases { get; set; }

        /// <summary>Values: 1..3, Used if numberPhases=1 and if the EVSE is capable of switching the phase connected to the EV, i.e. ACPhaseSwitchingSupported is defined and true. It’s not allowed unless both conditions above are true. If both conditions are true, and phaseToUse is omitted, the Charging Station / EVSE will make the selection on its own.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("phaseToUse", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, 3)]
        public int? PhaseToUse { get; set; }

        /// <summary>*(2.1)* Limit in _chargingRateUnit_ that the EV is allowed to discharge with. Note, these are negative values in order to be consistent with _setpoint_, which can be positive and negative.  +
        /// For AC this field represents the sum of all phases, unless values are provided for L2 and L3, in which case this field represents phase L1.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("dischargeLimit", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(double.MinValue, 0.0D)]
        public double? DischargeLimit { get; set; }

        /// <summary>*(2.1)* Limit in _chargingRateUnit_ on phase L2 that the EV is allowed to discharge with. 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("dischargeLimit_L2", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(double.MinValue, 0.0D)]
        public double? DischargeLimit_L2 { get; set; }

        /// <summary>*(2.1)* Limit in _chargingRateUnit_ on phase L3 that the EV is allowed to discharge with. 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("dischargeLimit_L3", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(double.MinValue, 0.0D)]
        public double? DischargeLimit_L3 { get; set; }

        /// <summary>*(2.1)* Setpoint in _chargingRateUnit_ that the EV should follow as close as possible. Use negative values for discharging. +
        /// When a limit and/or _dischargeLimit_ are given the overshoot when following _setpoint_ must remain within these values.
        /// This field represents the sum of all phases, unless values are provided for L2 and L3, in which case this field represents phase L1.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("setpoint", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? Setpoint { get; set; }

        /// <summary>*(2.1)* Setpoint in _chargingRateUnit_ that the EV should follow on phase L2 as close as possible.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("setpoint_L2", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? Setpoint_L2 { get; set; }

        /// <summary>*(2.1)* Setpoint in _chargingRateUnit_ that the EV should follow on phase L3 as close as possible. 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("setpoint_L3", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? Setpoint_L3 { get; set; }

        /// <summary>*(2.1)* Setpoint for reactive power (or current) in _chargingRateUnit_ that the EV should follow as closely as possible. Positive values for inductive, negative for capacitive reactive power or current.  +
        /// This field represents the sum of all phases, unless values are provided for L2 and L3, in which case this field represents phase L1.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("setpointReactive", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? SetpointReactive { get; set; }

        /// <summary>*(2.1)* Setpoint for reactive power (or current) in _chargingRateUnit_ that the EV should follow on phase L2 as closely as possible. 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("setpointReactive_L2", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? SetpointReactive_L2 { get; set; }

        /// <summary>*(2.1)* Setpoint for reactive power (or current) in _chargingRateUnit_ that the EV should follow on phase L3 as closely as possible. 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("setpointReactive_L3", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? SetpointReactive_L3 { get; set; }

        /// <summary>*(2.1)* If  true, the EV should attempt to keep the BMS preconditioned for this time interval.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("preconditioningRequest", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? PreconditioningRequest { get; set; }

        /// <summary>*(2.1)* If true, the EVSE must turn off power electronics/modules associated with this transaction. Default value when absent is false.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("evseSleep", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? EvseSleep { get; set; }

        /// <summary>*(2.1)* Power value that, when present, is used as a baseline on top of which values from _v2xFreqWattCurve_ and _v2xSignalWattCurve_ are added.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("v2xBaseline", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? V2xBaseline { get; set; }

        [Newtonsoft.Json.JsonProperty("operationMode", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public OperationModeEnumType? OperationMode { get; set; }

        [Newtonsoft.Json.JsonProperty("v2xFreqWattCurve", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(20)]
        public System.Collections.Generic.ICollection<V2XFreqWattPointType>? V2xFreqWattCurve { get; set; }

        [Newtonsoft.Json.JsonProperty("v2xSignalWattCurve", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(20)]
        public System.Collections.Generic.ICollection<V2XSignalWattPointType>? V2xSignalWattCurve { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Charging schedule structure defines a list of charging periods, as used in: NotifyEVChargingScheduleRequest and ChargingProfileType. When used in a NotifyEVChargingScheduleRequest only _duration_ and _chargingSchedulePeriod_ are relevant and _chargingRateUnit_ must be 'W'. +
    /// An ISO 15118-20 session may provide either an _absolutePriceSchedule_ or a _priceLevelSchedule_. An ISO 15118-2 session can only provide a_salesTariff_ element. The field _digestValue_ is used when price schedule or sales tariff are signed.
    /// 
    /// image::images/ChargingSchedule-Simple.png[]
    /// 
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ChargingScheduleType
    {
        [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.Always)]
        public int Id { get; set; }

        [Newtonsoft.Json.JsonProperty("limitAtSoC", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public LimitAtSoCType? LimitAtSoC { get; set; }

        /// <summary>Starting point of an absolute schedule or recurring schedule.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("startSchedule", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public System.DateTimeOffset? StartSchedule { get; set; }

        /// <summary>Duration of the charging schedule in seconds. If the duration is left empty, the last period will continue indefinitely or until end of the transaction in case startSchedule is absent.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("duration", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? Duration { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingRateUnit", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public ChargingRateUnitEnumType ChargingRateUnit { get; set; }

        /// <summary>Minimum charging rate supported by the EV. The unit of measure is defined by the chargingRateUnit. This parameter is intended to be used by a local smart charging algorithm to optimize the power allocation for in the case a charging process is inefficient at lower charging rates. 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("minChargingRate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? MinChargingRate { get; set; }

        /// <summary>*(2.1)* Power tolerance when following EVPowerProfile.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("powerTolerance", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? PowerTolerance { get; set; }

        /// <summary>*(2.1)* Id of this element for referencing in a signature.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("signatureId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int? SignatureId { get; set; }

        /// <summary>*(2.1)* Base64 encoded hash (SHA256 for ISO 15118-2, SHA512 for ISO 15118-20) of the EXI price schedule element. Used in signature.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("digestValue", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(88)]
        public string? DigestValue { get; set; }

        /// <summary>*(2.1)* Defaults to false. When true, disregard time zone offset in dateTime fields of  _ChargingScheduleType_ and use unqualified local time at Charging Station instead.
        ///  This allows the same `Absolute` or `Recurring` charging profile to be used in both summer and winter time.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("useLocalTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? UseLocalTime { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingSchedulePeriod", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(1024)]
        public System.Collections.Generic.ICollection<ChargingSchedulePeriodType> ChargingSchedulePeriod { get; set; } = new System.Collections.ObjectModel.Collection<ChargingSchedulePeriodType>();

        /// <summary>*(2.1)* Defaults to 0. When _randomizedDelay_ not equals zero, then the start of each &amp;lt;&amp;lt;cmn_chargingscheduleperiodtype,ChargingSchedulePeriodType&amp;gt;&amp;gt; is delayed by a randomly chosen number of seconds between 0 and _randomizedDelay_.  Only allowed for TxProfile and TxDefaultProfile.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("randomizedDelay", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int? RandomizedDelay { get; set; }

        [Newtonsoft.Json.JsonProperty("salesTariff", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public SalesTariffType? SalesTariff { get; set; }

        [Newtonsoft.Json.JsonProperty("absolutePriceSchedule", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public AbsolutePriceScheduleType? AbsolutePriceSchedule { get; set; }

        [Newtonsoft.Json.JsonProperty("priceLevelSchedule", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PriceLevelScheduleType? PriceLevelSchedule { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ConsumptionCostType
    {
        /// <summary>The lowest level of consumption that defines the starting point of this consumption block. The block interval extends to the start of the next interval.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("startValue", Required = Newtonsoft.Json.Required.Always)]
        public double StartValue { get; set; }

        [Newtonsoft.Json.JsonProperty("cost", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(3)]
        public System.Collections.Generic.ICollection<CostType> Cost { get; set; } = new System.Collections.ObjectModel.Collection<CostType>();

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class CostType
    {
        [Newtonsoft.Json.JsonProperty("costKind", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public CostKindEnumType CostKind { get; set; }

        /// <summary>The estimated or actual cost per kWh
        /// </summary>
        [Newtonsoft.Json.JsonProperty("amount", Required = Newtonsoft.Json.Required.Always)]
        public int Amount { get; set; }

        /// <summary>Values: -3..3, The amountMultiplier defines the exponent to base 10 (dec). The final value is determined by: amount * 10 ^ amountMultiplier
        /// </summary>
        [Newtonsoft.Json.JsonProperty("amountMultiplier", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? AmountMultiplier { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class LimitAtSoCType
    {
        /// <summary>The SoC value beyond which the charging rate limit should be applied.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("soc", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Range(0, 100)]
        public int Soc { get; set; }

        /// <summary>Charging rate limit beyond the SoC value.
        /// The unit is defined by _chargingSchedule.chargingRateUnit_.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("limit", Required = Newtonsoft.Json.Required.Always)]
        public double Limit { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Part of ISO 15118-20 price schedule.
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class OverstayRuleListType
    {
        [Newtonsoft.Json.JsonProperty("overstayPowerThreshold", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public RationalNumberType? OverstayPowerThreshold { get; set; }

        [Newtonsoft.Json.JsonProperty("overstayRule", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(5)]
        public System.Collections.Generic.ICollection<OverstayRuleType> OverstayRule { get; set; } = new System.Collections.ObjectModel.Collection<OverstayRuleType>();

        /// <summary>Time till overstay is applied in seconds.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("overstayTimeThreshold", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? OverstayTimeThreshold { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Part of ISO 15118-20 price schedule.
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class OverstayRuleType
    {
        [Newtonsoft.Json.JsonProperty("overstayFee", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public RationalNumberType OverstayFee { get; set; } = new RationalNumberType();

        /// <summary>Human readable string to identify the overstay rule.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("overstayRuleDescription", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(32)]
        public string? OverstayRuleDescription { get; set; }

        /// <summary>Time in seconds after trigger of the parent Overstay Rules for this particular fee to apply.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("startTime", Required = Newtonsoft.Json.Required.Always)]
        public int StartTime { get; set; }

        /// <summary>Time till overstay will be reapplied
        /// </summary>
        [Newtonsoft.Json.JsonProperty("overstayFeePeriod", Required = Newtonsoft.Json.Required.Always)]
        public int OverstayFeePeriod { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Part of ISO 15118-20 price schedule.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class PriceLevelScheduleEntryType
    {
        /// <summary>The amount of seconds that define the duration of this given PriceLevelScheduleEntry.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("duration", Required = Newtonsoft.Json.Required.Always)]
        public int Duration { get; set; }

        /// <summary>Defines the price level of this PriceLevelScheduleEntry (referring to NumberOfPriceLevels). Small values for the PriceLevel represent a cheaper PriceLevelScheduleEntry. Large values for the PriceLevel represent a more expensive PriceLevelScheduleEntry.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("priceLevel", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int PriceLevel { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>The PriceLevelScheduleType is modeled after the same type that is defined in ISO 15118-20, such that if it is supplied by an EMSP as a signed EXI message, the conversion from EXI to JSON (in OCPP) and back to EXI (for ISO 15118-20) does not change the digest and therefore does not invalidate the signature.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class PriceLevelScheduleType
    {
        [Newtonsoft.Json.JsonProperty("priceLevelScheduleEntries", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public System.Collections.Generic.ICollection<PriceLevelScheduleEntryType> PriceLevelScheduleEntries { get; set; } = new System.Collections.ObjectModel.Collection<PriceLevelScheduleEntryType>();

        /// <summary>Starting point of this price schedule.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("timeAnchor", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset TimeAnchor { get; set; }

        /// <summary>Unique ID of this price schedule.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("priceScheduleId", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int PriceScheduleId { get; set; }

        /// <summary>Description of the price schedule.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("priceScheduleDescription", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(32)]
        public string? PriceScheduleDescription { get; set; }

        /// <summary>Defines the overall number of distinct price level elements used across all PriceLevelSchedules.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("numberOfPriceLevels", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int NumberOfPriceLevels { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Part of ISO 15118-20 price schedule.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class PriceRuleStackType
    {
        /// <summary>Duration of the stack of price rules.  he amount of seconds that define the duration of the given PriceRule(s).
        /// </summary>
        [Newtonsoft.Json.JsonProperty("duration", Required = Newtonsoft.Json.Required.Always)]
        public int Duration { get; set; }

        [Newtonsoft.Json.JsonProperty("priceRule", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(8)]
        public System.Collections.Generic.ICollection<PriceRuleType> PriceRule { get; set; } = new System.Collections.ObjectModel.Collection<PriceRuleType>();

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Part of ISO 15118-20 price schedule.
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class PriceRuleType
    {
        /// <summary>The duration of the parking fee period (in seconds).
        /// When the time enters into a ParkingFeePeriod, the ParkingFee will apply to the session. .
        /// </summary>
        [Newtonsoft.Json.JsonProperty("parkingFeePeriod", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? ParkingFeePeriod { get; set; }

        /// <summary>Number of grams of CO2 per kWh.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("carbonDioxideEmission", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int? CarbonDioxideEmission { get; set; }

        /// <summary>Percentage of the power that is created by renewable resources.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("renewableGenerationPercentage", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, 100)]
        public int? RenewableGenerationPercentage { get; set; }

        [Newtonsoft.Json.JsonProperty("energyFee", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public RationalNumberType EnergyFee { get; set; } = new RationalNumberType();

        [Newtonsoft.Json.JsonProperty("parkingFee", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public RationalNumberType? ParkingFee { get; set; }

        [Newtonsoft.Json.JsonProperty("powerRangeStart", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public RationalNumberType PowerRangeStart { get; set; } = new RationalNumberType();

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Part of ISO 15118-20 price schedule.
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class RationalNumberType
    {
        /// <summary>The exponent to base 10 (dec)
        /// </summary>
        [Newtonsoft.Json.JsonProperty("exponent", Required = Newtonsoft.Json.Required.Always)]
        public int Exponent { get; set; }

        /// <summary>Value which shall be multiplied.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("value", Required = Newtonsoft.Json.Required.Always)]
        public int Value { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class RelativeTimeIntervalType
    {
        /// <summary>Start of the interval, in seconds from NOW.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("start", Required = Newtonsoft.Json.Required.Always)]
        public int Start { get; set; }

        /// <summary>Duration of the interval, in seconds.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("duration", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? Duration { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class SalesTariffEntryType
    {
        [Newtonsoft.Json.JsonProperty("relativeTimeInterval", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public RelativeTimeIntervalType RelativeTimeInterval { get; set; } = new RelativeTimeIntervalType();

        /// <summary>Defines the price level of this SalesTariffEntry (referring to NumEPriceLevels). Small values for the EPriceLevel represent a cheaper TariffEntry. Large values for the EPriceLevel represent a more expensive TariffEntry.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("ePriceLevel", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int? EPriceLevel { get; set; }

        [Newtonsoft.Json.JsonProperty("consumptionCost", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(3)]
        public System.Collections.Generic.ICollection<ConsumptionCostType>? ConsumptionCost { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>A SalesTariff provided by a Mobility Operator (EMSP) .
    /// NOTE: This dataType is based on dataTypes from &amp;lt;&amp;lt;ref-ISOIEC15118-2,ISO 15118-2&amp;gt;&amp;gt;.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class SalesTariffType
    {
        /// <summary>SalesTariff identifier used to identify one sales tariff. An SAID remains a unique identifier for one schedule throughout a charging session.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int Id { get; set; }

        /// <summary>A human readable title/short description of the sales tariff e.g. for HMI display purposes.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("salesTariffDescription", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(32)]
        public string? SalesTariffDescription { get; set; }

        /// <summary>Defines the overall number of distinct price levels used across all provided SalesTariff elements.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("numEPriceLevels", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int? NumEPriceLevels { get; set; }

        [Newtonsoft.Json.JsonProperty("salesTariffEntry", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(1024)]
        public System.Collections.Generic.ICollection<SalesTariffEntryType> SalesTariffEntry { get; set; } = new System.Collections.ObjectModel.Collection<SalesTariffEntryType>();

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Part of ISO 15118-20 price schedule.
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class TaxRuleType
    {
        /// <summary>Id for the tax rule.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("taxRuleID", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int TaxRuleID { get; set; }

        /// <summary>Human readable string to identify the tax rule.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("taxRuleName", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string? TaxRuleName { get; set; }

        /// <summary>Indicates whether the tax is included in any price or not.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("taxIncludedInPrice", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? TaxIncludedInPrice { get; set; }

        /// <summary>Indicates whether this tax applies to Energy Fees.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("appliesToEnergyFee", Required = Newtonsoft.Json.Required.Always)]
        public bool AppliesToEnergyFee { get; set; }

        /// <summary>Indicates whether this tax applies to Parking Fees.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("appliesToParkingFee", Required = Newtonsoft.Json.Required.Always)]
        public bool AppliesToParkingFee { get; set; }

        /// <summary>Indicates whether this tax applies to Overstay Fees.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("appliesToOverstayFee", Required = Newtonsoft.Json.Required.Always)]
        public bool AppliesToOverstayFee { get; set; }

        /// <summary>Indicates whether this tax applies to Minimum/Maximum Cost.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("appliesToMinimumMaximumCost", Required = Newtonsoft.Json.Required.Always)]
        public bool AppliesToMinimumMaximumCost { get; set; }

        [Newtonsoft.Json.JsonProperty("taxRate", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public RationalNumberType TaxRate { get; set; } = new RationalNumberType();

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>*(2.1)* A point of a frequency-watt curve.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class V2XFreqWattPointType
    {
        /// <summary>Net frequency in Hz.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("frequency", Required = Newtonsoft.Json.Required.Always)]
        public double Frequency { get; set; }

        /// <summary>Power in W to charge (positive) or discharge (negative) at specified frequency.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("power", Required = Newtonsoft.Json.Required.Always)]
        public double Power { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>*(2.1)* A point of a signal-watt curve.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class V2XSignalWattPointType
    {
        /// <summary>Signal value from an AFRRSignalRequest.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("signal", Required = Newtonsoft.Json.Required.Always)]
        public int Signal { get; set; }

        /// <summary>Power in W to charge (positive) or discharge (negative) at specified frequency.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("power", Required = Newtonsoft.Json.Required.Always)]
        public double Power { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }


    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class NotifyChargingLimitRequest
    {
        [Newtonsoft.Json.JsonProperty("chargingSchedule", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.ICollection<ChargingScheduleType>? ChargingSchedule { get; set; }

        /// <summary>The EVSE to which the charging limit is set. If absent or when zero, it applies to the entire Charging Station.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("evseId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int? EvseId { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingLimit", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public ChargingLimitType ChargingLimit { get; set; } = new ChargingLimitType();

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }
}