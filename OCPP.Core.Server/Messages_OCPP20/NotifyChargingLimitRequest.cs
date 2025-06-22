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

    /// <summary>Charging_ Schedule. Charging_ Rate_ Unit. Charging_ Rate_ Unit_ Code
    /// urn:x-oca:ocpp:uid:1:569238
    /// The unit of measure Limit is expressed in.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum ChargingRateUnitEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"W")]
        W = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"A")]
        A = 1,

    }

    /// <summary>Cost. Cost_ Kind. Cost_ Kind_ Code
    /// urn:x-oca:ocpp:uid:1:569243
    /// The kind of cost referred to in the message element amount
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

    /// <summary>Charging_ Limit
    /// urn:x-enexis:ecdm:uid:2:234489
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ChargingLimitType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingLimitSource", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public ChargingLimitSourceEnumType ChargingLimitSource { get; set; }

        /// <summary>Charging_ Limit. Is_ Grid_ Critical. Indicator
        /// urn:x-enexis:ecdm:uid:1:570847
        /// Indicates whether the charging limit is critical for the grid.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("isGridCritical", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? IsGridCritical { get; set; }


    }

    /// <summary>Charging_ Schedule_ Period
    /// urn:x-oca:ocpp:uid:2:233257
    /// Charging schedule period structure defines a time period in a charging schedule.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ChargingSchedulePeriodType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        /// <summary>Charging_ Schedule_ Period. Start_ Period. Elapsed_ Time
        /// urn:x-oca:ocpp:uid:1:569240
        /// Start of the period, in seconds from the start of schedule. The value of StartPeriod also defines the stop time of the previous period.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("startPeriod", Required = Newtonsoft.Json.Required.Always)]
        public int StartPeriod { get; set; }

        /// <summary>Charging_ Schedule_ Period. Limit. Measure
        /// urn:x-oca:ocpp:uid:1:569241
        /// Charging rate limit during the schedule period, in the applicable chargingRateUnit, for example in Amperes (A) or Watts (W). Accepts at most one digit fraction (e.g. 8.1).
        /// </summary>
        [Newtonsoft.Json.JsonProperty("limit", Required = Newtonsoft.Json.Required.Always)]
        public double Limit { get; set; }

        /// <summary>Charging_ Schedule_ Period. Number_ Phases. Counter
        /// urn:x-oca:ocpp:uid:1:569242
        /// The number of phases that can be used for charging. If a number of phases is needed, numberPhases=3 will be assumed unless another number is given.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("numberPhases", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? NumberPhases { get; set; }

        /// <summary>Values: 1..3, Used if numberPhases=1 and if the EVSE is capable of switching the phase connected to the EV, i.e. ACPhaseSwitchingSupported is defined and true. It’s not allowed unless both conditions above are true. If both conditions are true, and phaseToUse is omitted, the Charging Station / EVSE will make the selection on its own.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("phaseToUse", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? PhaseToUse { get; set; }


    }

    /// <summary>Charging_ Schedule
    /// urn:x-oca:ocpp:uid:2:233256
    /// Charging schedule structure defines a list of charging periods, as used in: GetCompositeSchedule.conf and ChargingProfile. 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ChargingScheduleType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        /// <summary>Identifies the ChargingSchedule.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.Always)]
        public int Id { get; set; }

        /// <summary>Charging_ Schedule. Start_ Schedule. Date_ Time
        /// urn:x-oca:ocpp:uid:1:569237
        /// Starting point of an absolute schedule. If absent the schedule will be relative to start of charging.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("startSchedule", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public System.DateTimeOffset? StartSchedule { get; set; }

        /// <summary>Charging_ Schedule. Duration. Elapsed_ Time
        /// urn:x-oca:ocpp:uid:1:569236
        /// Duration of the charging schedule in seconds. If the duration is left empty, the last period will continue indefinitely or until end of the transaction if chargingProfilePurpose = TxProfile.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("duration", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? Duration { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingRateUnit", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public ChargingRateUnitEnumType ChargingRateUnit { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingSchedulePeriod", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(1024)]
        public System.Collections.Generic.ICollection<ChargingSchedulePeriodType> ChargingSchedulePeriod { get; set; } = new System.Collections.ObjectModel.Collection<ChargingSchedulePeriodType>();

        /// <summary>Charging_ Schedule. Min_ Charging_ Rate. Numeric
        /// urn:x-oca:ocpp:uid:1:569239
        /// Minimum charging rate supported by the EV. The unit of measure is defined by the chargingRateUnit. This parameter is intended to be used by a local smart charging algorithm to optimize the power allocation for in the case a charging process is inefficient at lower charging rates. Accepts at most one digit fraction (e.g. 8.1)
        /// </summary>
        [Newtonsoft.Json.JsonProperty("minChargingRate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? MinChargingRate { get; set; }

        [Newtonsoft.Json.JsonProperty("salesTariff", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public SalesTariffType? SalesTariff { get; set; }
    }

    /// <summary>Consumption_ Cost
    /// urn:x-oca:ocpp:uid:2:233259
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ConsumptionCostType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        /// <summary>Consumption_ Cost. Start_ Value. Numeric
        /// urn:x-oca:ocpp:uid:1:569246
        /// The lowest level of consumption that defines the starting point of this consumption block. The block interval extends to the start of the next interval.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("startValue", Required = Newtonsoft.Json.Required.Always)]
        public double StartValue { get; set; }

        [Newtonsoft.Json.JsonProperty("cost", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(3)]
        public System.Collections.Generic.ICollection<CostType> Cost { get; set; } = new System.Collections.ObjectModel.Collection<CostType>();
    }

    /// <summary>Cost
    /// urn:x-oca:ocpp:uid:2:233258
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class CostType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("costKind", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public CostKindEnumType CostKind { get; set; }

        /// <summary>Cost. Amount. Amount
        /// urn:x-oca:ocpp:uid:1:569244
        /// The estimated or actual cost per kWh
        /// </summary>
        [Newtonsoft.Json.JsonProperty("amount", Required = Newtonsoft.Json.Required.Always)]
        public int Amount { get; set; }

        /// <summary>Cost. Amount_ Multiplier. Integer
        /// urn:x-oca:ocpp:uid:1:569245
        /// Values: -3..3, The amountMultiplier defines the exponent to base 10 (dec). The final value is determined by: amount * 10 ^ amountMultiplier
        /// </summary>
        [Newtonsoft.Json.JsonProperty("amountMultiplier", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? AmountMultiplier { get; set; }
    }

    /// <summary>Relative_ Timer_ Interval
    /// urn:x-oca:ocpp:uid:2:233270
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class RelativeTimeIntervalType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        /// <summary>Relative_ Timer_ Interval. Start. Elapsed_ Time
        /// urn:x-oca:ocpp:uid:1:569279
        /// Start of the interval, in seconds from NOW.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("start", Required = Newtonsoft.Json.Required.Always)]
        public int Start { get; set; }

        /// <summary>Relative_ Timer_ Interval. Duration. Elapsed_ Time
        /// urn:x-oca:ocpp:uid:1:569280
        /// Duration of the interval, in seconds.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("duration", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? Duration { get; set; }
    }

    /// <summary>Sales_ Tariff_ Entry
    /// urn:x-oca:ocpp:uid:2:233271
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class SalesTariffEntryType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("relativeTimeInterval", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public RelativeTimeIntervalType RelativeTimeInterval { get; set; } = new RelativeTimeIntervalType();

        /// <summary>Sales_ Tariff_ Entry. E_ Price_ Level. Unsigned_ Integer
        /// urn:x-oca:ocpp:uid:1:569281
        /// Defines the price level of this SalesTariffEntry (referring to NumEPriceLevels). Small values for the EPriceLevel represent a cheaper TariffEntry. Large values for the EPriceLevel represent a more expensive TariffEntry.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("ePriceLevel", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int? EPriceLevel { get; set; }

        [Newtonsoft.Json.JsonProperty("consumptionCost", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(3)]
        public System.Collections.Generic.ICollection<ConsumptionCostType>? ConsumptionCost { get; set; }
    }

    /// <summary>Sales_ Tariff
    /// urn:x-oca:ocpp:uid:2:233272
    /// NOTE: This dataType is based on dataTypes from &amp;lt;&amp;lt;ref-ISOIEC15118-2,ISO 15118-2&amp;gt;&amp;gt;.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class SalesTariffType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        /// <summary>Identified_ Object. MRID. Numeric_ Identifier
        /// urn:x-enexis:ecdm:uid:1:569198
        /// SalesTariff identifier used to identify one sales tariff. An SAID remains a unique identifier for one schedule throughout a charging session.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.Always)]
        public int Id { get; set; }

        /// <summary>Sales_ Tariff. Sales. Tariff_ Description
        /// urn:x-oca:ocpp:uid:1:569283
        /// A human readable title/short description of the sales tariff e.g. for HMI display purposes.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("salesTariffDescription", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(32)]
        public string? SalesTariffDescription { get; set; }

        /// <summary>Sales_ Tariff. Num_ E_ Price_ Levels. Counter
        /// urn:x-oca:ocpp:uid:1:569284
        /// Defines the overall number of distinct price levels used across all provided SalesTariff elements.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("numEPriceLevels", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? NumEPriceLevels { get; set; }

        [Newtonsoft.Json.JsonProperty("salesTariffEntry", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(1024)]
        public System.Collections.Generic.ICollection<SalesTariffEntryType> SalesTariffEntry { get; set; } = new System.Collections.ObjectModel.Collection<SalesTariffEntryType>();
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class NotifyChargingLimitRequest
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingSchedule", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.ICollection<ChargingScheduleType>? ChargingSchedule { get; set; }

        /// <summary>The charging schedule contained in this notification applies to an EVSE. evseId must be &amp;gt; 0.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("evseId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? EvseId { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingLimit", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public ChargingLimitType ChargingLimit { get; set; } = new ChargingLimitType();
    }
}