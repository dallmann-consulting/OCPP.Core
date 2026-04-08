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

    /// <summary>Current charging state, is required when state
    /// has changed. Omitted when there is no communication between EVSE and EV, because no cable is plugged in.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum ChargingStateEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"EVConnected")]
        EVConnected = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Charging")]
        Charging = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"SuspendedEV")]
        SuspendedEV = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"SuspendedEVSE")]
        SuspendedEVSE = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"Idle")]
        Idle = 4,

    }

    /// <summary>Type of cost dimension: energy, power, time, etc.
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum CostDimensionEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Energy")]
        Energy = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"MaxCurrent")]
        MaxCurrent = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"MinCurrent")]
        MinCurrent = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"MaxPower")]
        MaxPower = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"MinPower")]
        MinPower = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"IdleTIme")]
        IdleTIme = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"ChargingTime")]
        ChargingTime = 6,

    }


    /// <summary>*(2.1)* The _operationMode_ that is currently in effect for the transaction.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum OperationModeEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Idle")]
        Idle = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"ChargingOnly")]
        ChargingOnly = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"CentralSetpoint")]
        CentralSetpoint = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"ExternalSetpoint")]
        ExternalSetpoint = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"ExternalLimits")]
        ExternalLimits = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"CentralFrequency")]
        CentralFrequency = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"LocalFrequency")]
        LocalFrequency = 6,

        [System.Runtime.Serialization.EnumMember(Value = @"LocalLoadBalancing")]
        LocalLoadBalancing = 7,

    }


    /// <summary>*(2.1)* The current preconditioning status of the BMS in the EV. Default value is Unknown.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum PreconditioningStatusEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Unknown")]
        Unknown = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Ready")]
        Ready = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"NotReady")]
        NotReady = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"Preconditioning")]
        Preconditioning = 3,

    }


    /// <summary>The _stoppedReason_ is the reason/event that initiated the process of stopping the transaction. It will normally be the user stopping authorization via card (Local or MasterPass) or app (Remote), but it can also be CSMS revoking authorization (DeAuthorized), or disconnecting the EV when TxStopPoint = EVConnected (EVDisconnected). Most other reasons are related to technical faults or energy limitations. +
    /// MAY only be omitted when _stoppedReason_ is "Local"
    /// 
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum ReasonEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"DeAuthorized")]
        DeAuthorized = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"EmergencyStop")]
        EmergencyStop = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"EnergyLimitReached")]
        EnergyLimitReached = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"EVDisconnected")]
        EVDisconnected = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"GroundFault")]
        GroundFault = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"ImmediateReset")]
        ImmediateReset = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"MasterPass")]
        MasterPass = 6,

        [System.Runtime.Serialization.EnumMember(Value = @"Local")]
        Local = 7,

        [System.Runtime.Serialization.EnumMember(Value = @"LocalOutOfCredit")]
        LocalOutOfCredit = 8,

        [System.Runtime.Serialization.EnumMember(Value = @"Other")]
        Other = 9,

        [System.Runtime.Serialization.EnumMember(Value = @"OvercurrentFault")]
        OvercurrentFault = 10,

        [System.Runtime.Serialization.EnumMember(Value = @"PowerLoss")]
        PowerLoss = 11,

        [System.Runtime.Serialization.EnumMember(Value = @"PowerQuality")]
        PowerQuality = 12,

        [System.Runtime.Serialization.EnumMember(Value = @"Reboot")]
        Reboot = 13,

        [System.Runtime.Serialization.EnumMember(Value = @"Remote")]
        Remote = 14,

        [System.Runtime.Serialization.EnumMember(Value = @"SOCLimitReached")]
        SOCLimitReached = 15,

        [System.Runtime.Serialization.EnumMember(Value = @"StoppedByEV")]
        StoppedByEV = 16,

        [System.Runtime.Serialization.EnumMember(Value = @"TimeLimitReached")]
        TimeLimitReached = 17,

        [System.Runtime.Serialization.EnumMember(Value = @"Timeout")]
        Timeout = 18,

        [System.Runtime.Serialization.EnumMember(Value = @"ReqEnergyTransferRejected")]
        ReqEnergyTransferRejected = 19,

    }

    /// <summary>Type of cost: normal or the minimum or maximum cost.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum TariffCostEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"NormalCost")]
        NormalCost = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"MinCost")]
        MinCost = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"MaxCost")]
        MaxCost = 2,

    }

    /// <summary>This contains the type of this event.
    /// The first TransactionEvent of a transaction SHALL contain: "Started" The last TransactionEvent of a transaction SHALL contain: "Ended" All others SHALL contain: "Updated"
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum TransactionEventEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Ended")]
        Ended = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Started")]
        Started = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"Updated")]
        Updated = 2,

    }

    /// <summary>Reason the Charging Station sends this message to the CSMS
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum TriggerReasonEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"AbnormalCondition")]
        AbnormalCondition = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Authorized")]
        Authorized = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"CablePluggedIn")]
        CablePluggedIn = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"ChargingRateChanged")]
        ChargingRateChanged = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"ChargingStateChanged")]
        ChargingStateChanged = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"CostLimitReached")]
        CostLimitReached = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"Deauthorized")]
        Deauthorized = 6,

        [System.Runtime.Serialization.EnumMember(Value = @"EnergyLimitReached")]
        EnergyLimitReached = 7,

        [System.Runtime.Serialization.EnumMember(Value = @"EVCommunicationLost")]
        EVCommunicationLost = 8,

        [System.Runtime.Serialization.EnumMember(Value = @"EVConnectTimeout")]
        EVConnectTimeout = 9,

        [System.Runtime.Serialization.EnumMember(Value = @"EVDeparted")]
        EVDeparted = 10,

        [System.Runtime.Serialization.EnumMember(Value = @"EVDetected")]
        EVDetected = 11,

        [System.Runtime.Serialization.EnumMember(Value = @"LimitSet")]
        LimitSet = 12,

        [System.Runtime.Serialization.EnumMember(Value = @"MeterValueClock")]
        MeterValueClock = 13,

        [System.Runtime.Serialization.EnumMember(Value = @"MeterValuePeriodic")]
        MeterValuePeriodic = 14,

        [System.Runtime.Serialization.EnumMember(Value = @"OperationModeChanged")]
        OperationModeChanged = 15,

        [System.Runtime.Serialization.EnumMember(Value = @"RemoteStart")]
        RemoteStart = 16,

        [System.Runtime.Serialization.EnumMember(Value = @"RemoteStop")]
        RemoteStop = 17,

        [System.Runtime.Serialization.EnumMember(Value = @"ResetCommand")]
        ResetCommand = 18,

        [System.Runtime.Serialization.EnumMember(Value = @"RunningCost")]
        RunningCost = 19,

        [System.Runtime.Serialization.EnumMember(Value = @"SignedDataReceived")]
        SignedDataReceived = 20,

        [System.Runtime.Serialization.EnumMember(Value = @"SoCLimitReached")]
        SoCLimitReached = 21,

        [System.Runtime.Serialization.EnumMember(Value = @"StopAuthorized")]
        StopAuthorized = 22,

        [System.Runtime.Serialization.EnumMember(Value = @"TariffChanged")]
        TariffChanged = 23,

        [System.Runtime.Serialization.EnumMember(Value = @"TariffNotAccepted")]
        TariffNotAccepted = 24,

        [System.Runtime.Serialization.EnumMember(Value = @"TimeLimitReached")]
        TimeLimitReached = 25,

        [System.Runtime.Serialization.EnumMember(Value = @"Trigger")]
        Trigger = 26,

        [System.Runtime.Serialization.EnumMember(Value = @"TxResumed")]
        TxResumed = 27,

        [System.Runtime.Serialization.EnumMember(Value = @"UnlockCommand")]
        UnlockCommand = 28,

    }


    /// <summary>A ChargingPeriodType consists of a start time, and a list of possible values that influence this period, for example: amount of energy charged this period, maximum current during this period etc.
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class ChargingPeriodType
    {
        [Newtonsoft.Json.JsonProperty("dimensions", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.ICollection<CostDimensionType>? Dimensions { get; set; }

        /// <summary>Unique identifier of the Tariff that was used to calculate cost. If not provided, then cost was calculated by some other means.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("tariffId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(60)]
        public string? TariffId { get; set; }

        /// <summary>Start timestamp of charging period. A period ends when the next period starts. The last period ends when the session ends.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("startPeriod", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset StartPeriod { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>CostDetailsType contains the cost as calculated by Charging Station based on provided TariffType.
    /// 
    /// NOTE: Reservation is not shown as a _chargingPeriod_, because it took place outside of the transaction.
    /// 
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class CostDetailsType
    {
        [Newtonsoft.Json.JsonProperty("chargingPeriods", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.ICollection<ChargingPeriodType>? ChargingPeriods { get; set; }

        [Newtonsoft.Json.JsonProperty("totalCost", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public TotalCostType TotalCost { get; set; } = new TotalCostType();

        [Newtonsoft.Json.JsonProperty("totalUsage", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public TotalUsageType TotalUsage { get; set; } = new TotalUsageType();

        /// <summary>If set to true, then Charging Station has failed to calculate the cost.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("failureToCalculate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? FailureToCalculate { get; set; }

        /// <summary>Optional human-readable reason text in case of failure to calculate.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("failureReason", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string? FailureReason { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Volume consumed of cost dimension.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class CostDimensionType
    {
        [Newtonsoft.Json.JsonProperty("type", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public CostDimensionEnumType Type { get; set; }

        /// <summary>Volume of the dimension consumed, measured according to the dimension type.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("volume", Required = Newtonsoft.Json.Required.Always)]
        public double Volume { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Electric Vehicle Supply Equipment
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class EVSEType
    {
        /// <summary>EVSE Identifier. This contains a number (&amp;gt; 0) designating an EVSE of the Charging Station.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int Id { get; set; }

        /// <summary>An id to designate a specific connector (on an EVSE) by connector index number.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("connectorId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int? ConnectorId { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }


    /// <summary>This contains the cost calculated during a transaction. It is used both for running cost and final cost of the transaction.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class TotalCostType
    {
        /// <summary>Currency of the costs in ISO 4217 Code.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("currency", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(3)]
        public string Currency { get; set; }

        [Newtonsoft.Json.JsonProperty("typeOfCost", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public TariffCostEnumType TypeOfCost { get; set; }

        [Newtonsoft.Json.JsonProperty("fixed", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PriceType? Fixed { get; set; }

        [Newtonsoft.Json.JsonProperty("energy", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PriceType? Energy { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PriceType? ChargingTime { get; set; }

        [Newtonsoft.Json.JsonProperty("idleTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PriceType? IdleTime { get; set; }

        [Newtonsoft.Json.JsonProperty("reservationTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PriceType? ReservationTime { get; set; }

        [Newtonsoft.Json.JsonProperty("reservationFixed", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PriceType? ReservationFixed { get; set; }

        [Newtonsoft.Json.JsonProperty("total", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public TotalPriceType Total { get; set; } = new TotalPriceType();

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>Total cost with and without tax. Contains the total of energy, charging time, idle time, fixed and reservation costs including and/or excluding tax.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class TotalPriceType
    {
        /// <summary>Price/cost excluding tax. Can be absent if _inclTax_ is present.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("exclTax", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? ExclTax { get; set; }

        /// <summary>Price/cost including tax. Can be absent if _exclTax_ is present.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("inclTax", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? InclTax { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }

    /// <summary>This contains the calculated usage of energy, charging time and idle time during a transaction.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class TotalUsageType
    {
        [Newtonsoft.Json.JsonProperty("energy", Required = Newtonsoft.Json.Required.Always)]
        public double Energy { get; set; }

        /// <summary>Total duration of the charging session (including the duration of charging and not charging), in seconds.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("chargingTime", Required = Newtonsoft.Json.Required.Always)]
        public int ChargingTime { get; set; }

        /// <summary>Total duration of the charging session where the EV was not charging (no energy was transferred between EVSE and EV), in seconds.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("idleTime", Required = Newtonsoft.Json.Required.Always)]
        public int IdleTime { get; set; }

        /// <summary>Total time of reservation in seconds.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("reservationTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? ReservationTime { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }


    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class TransactionType
    {
        /// <summary>This contains the Id of the transaction.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("transactionId", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(36)]
        public string TransactionId { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingState", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public ChargingStateEnumType? ChargingState { get; set; }

        /// <summary>Contains the total time that energy flowed from EVSE to EV during the transaction (in seconds). Note that timeSpentCharging is smaller or equal to the duration of the transaction.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("timeSpentCharging", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? TimeSpentCharging { get; set; }

        [Newtonsoft.Json.JsonProperty("stoppedReason", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public ReasonEnumType? StoppedReason { get; set; }

        /// <summary>The ID given to remote start request (&amp;lt;&amp;lt;requeststarttransactionrequest, RequestStartTransactionRequest&amp;gt;&amp;gt;. This enables to CSMS to match the started transaction to the given start request.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("remoteStartId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? RemoteStartId { get; set; }

        [Newtonsoft.Json.JsonProperty("operationMode", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public OperationModeEnumType? OperationMode { get; set; }

        /// <summary>*(2.1)* Id of tariff in use for transaction
        /// </summary>
        [Newtonsoft.Json.JsonProperty("tariffId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(60)]
        public string? TariffId { get; set; }

        [Newtonsoft.Json.JsonProperty("transactionLimit", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public TransactionLimitType? TransactionLimit { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }


    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class TransactionEventRequest
    {
        [Newtonsoft.Json.JsonProperty("costDetails", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CostDetailsType? CostDetails { get; set; }

        [Newtonsoft.Json.JsonProperty("eventType", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public TransactionEventEnumType EventType { get; set; }

        [Newtonsoft.Json.JsonProperty("meterValue", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.ICollection<MeterValueType>? MeterValue { get; set; }

        /// <summary>The date and time at which this transaction event occurred.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("timestamp", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset Timestamp { get; set; }

        [Newtonsoft.Json.JsonProperty("triggerReason", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public TriggerReasonEnumType TriggerReason { get; set; }

        /// <summary>Incremental sequence number, helps with determining if all messages of a transaction have been received.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("seqNo", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int SeqNo { get; set; }

        /// <summary>Indication that this transaction event happened when the Charging Station was offline. Default = false, meaning: the event occurred when the Charging Station was online.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("offline", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? Offline { get; set; } = false;

        /// <summary>If the Charging Station is able to report the number of phases used, then it SHALL provide it.
        /// When omitted the CSMS may be able to determine the number of phases used as follows: +
        /// 1: The numberPhases in the currently used ChargingSchedule. +
        /// 2: The number of phases provided via device management.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("numberOfPhasesUsed", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, 3)]
        public int? NumberOfPhasesUsed { get; set; }

        /// <summary>The maximum current of the connected cable in Ampere (A).
        /// </summary>
        [Newtonsoft.Json.JsonProperty("cableMaxCurrent", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? CableMaxCurrent { get; set; }

        /// <summary>This contains the Id of the reservation that terminates as a result of this transaction.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("reservationId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int? ReservationId { get; set; }

        [Newtonsoft.Json.JsonProperty("preconditioningStatus", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public PreconditioningStatusEnumType? PreconditioningStatus { get; set; }

        /// <summary>*(2.1)* True when EVSE electronics are in sleep mode for this transaction. Default value (when absent) is false.
        /// 
        /// </summary>
        [Newtonsoft.Json.JsonProperty("evseSleep", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? EvseSleep { get; set; }

        [Newtonsoft.Json.JsonProperty("transactionInfo", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public TransactionType TransactionInfo { get; set; } = new TransactionType();

        [Newtonsoft.Json.JsonProperty("evse", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public EVSEType? Evse { get; set; }

        [Newtonsoft.Json.JsonProperty("idToken", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public IdTokenType? IdToken { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }
}