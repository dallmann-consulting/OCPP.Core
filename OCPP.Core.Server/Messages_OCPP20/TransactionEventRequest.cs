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

    /// <summary>Transaction. State. Transaction_ State_ Code
    /// urn:x-oca:ocpp:uid:1:569419
    /// Current charging state, is required when state
    /// has changed.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum ChargingStateEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Charging")]
        Charging = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"EVConnected")]
        EVConnected = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"SuspendedEV")]
        SuspendedEV = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"SuspendedEVSE")]
        SuspendedEVSE = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"Idle")]
        Idle = 4,
    }

    /// <summary>Transaction. Stopped_ Reason. EOT_ Reason_ Code
    /// urn:x-oca:ocpp:uid:1:569413
    /// This contains the reason why the transaction was stopped. MAY only be omitted when Reason is "Local".
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum ReasonEnumType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"")]
        Missing,

        [System.Runtime.Serialization.EnumMember(Value = @"DeAuthorized")]
        DeAuthorized,

        [System.Runtime.Serialization.EnumMember(Value = @"EmergencyStop")]
        EmergencyStop,

        [System.Runtime.Serialization.EnumMember(Value = @"EnergyLimitReached")]
        EnergyLimitReached,

        [System.Runtime.Serialization.EnumMember(Value = @"EVDisconnected")]
        EVDisconnected,

        [System.Runtime.Serialization.EnumMember(Value = @"GroundFault")]
        GroundFault,

        [System.Runtime.Serialization.EnumMember(Value = @"ImmediateReset")]
        ImmediateReset,

        [System.Runtime.Serialization.EnumMember(Value = @"Local")]
        Local,

        [System.Runtime.Serialization.EnumMember(Value = @"LocalOutOfCredit")]
        LocalOutOfCredit,

        [System.Runtime.Serialization.EnumMember(Value = @"MasterPass")]
        MasterPass,

        [System.Runtime.Serialization.EnumMember(Value = @"Other")]
        Other,

        [System.Runtime.Serialization.EnumMember(Value = @"OvercurrentFault")]
        OvercurrentFault,

        [System.Runtime.Serialization.EnumMember(Value = @"PowerLoss")]
        PowerLoss,

        [System.Runtime.Serialization.EnumMember(Value = @"PowerQuality")]
        PowerQuality,

        [System.Runtime.Serialization.EnumMember(Value = @"Reboot")]
        Reboot,

        [System.Runtime.Serialization.EnumMember(Value = @"Remote")]
        Remote,

        [System.Runtime.Serialization.EnumMember(Value = @"SOCLimitReached")]
        SOCLimitReached,

        [System.Runtime.Serialization.EnumMember(Value = @"StoppedByEV")]
        StoppedByEV,

        [System.Runtime.Serialization.EnumMember(Value = @"TimeLimitReached")]
        TimeLimitReached,

        [System.Runtime.Serialization.EnumMember(Value = @"Timeout")]
        Timeout
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
        [System.Runtime.Serialization.EnumMember(Value = @"Authorized")]
        Authorized = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"CablePluggedIn")]
        CablePluggedIn = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"ChargingRateChanged")]
        ChargingRateChanged = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"ChargingStateChanged")]
        ChargingStateChanged = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"Deauthorized")]
        Deauthorized = 4,

        [System.Runtime.Serialization.EnumMember(Value = @"EnergyLimitReached")]
        EnergyLimitReached = 5,

        [System.Runtime.Serialization.EnumMember(Value = @"EVCommunicationLost")]
        EVCommunicationLost = 6,

        [System.Runtime.Serialization.EnumMember(Value = @"EVConnectTimeout")]
        EVConnectTimeout = 7,

        [System.Runtime.Serialization.EnumMember(Value = @"MeterValueClock")]
        MeterValueClock = 8,

        [System.Runtime.Serialization.EnumMember(Value = @"MeterValuePeriodic")]
        MeterValuePeriodic = 9,

        [System.Runtime.Serialization.EnumMember(Value = @"TimeLimitReached")]
        TimeLimitReached = 10,

        [System.Runtime.Serialization.EnumMember(Value = @"Trigger")]
        Trigger = 11,

        [System.Runtime.Serialization.EnumMember(Value = @"UnlockCommand")]
        UnlockCommand = 12,

        [System.Runtime.Serialization.EnumMember(Value = @"StopAuthorized")]
        StopAuthorized = 13,

        [System.Runtime.Serialization.EnumMember(Value = @"EVDeparted")]
        EVDeparted = 14,

        [System.Runtime.Serialization.EnumMember(Value = @"EVDetected")]
        EVDetected = 15,

        [System.Runtime.Serialization.EnumMember(Value = @"RemoteStop")]
        RemoteStop = 16,

        [System.Runtime.Serialization.EnumMember(Value = @"RemoteStart")]
        RemoteStart = 17,

        [System.Runtime.Serialization.EnumMember(Value = @"AbnormalCondition")]
        AbnormalCondition = 18,

        [System.Runtime.Serialization.EnumMember(Value = @"SignedDataReceived")]
        SignedDataReceived = 19,

        [System.Runtime.Serialization.EnumMember(Value = @"ResetCommand")]
        ResetCommand = 20,
    }

    /// <summary>EVSE
    /// urn:x-oca:ocpp:uid:2:233123
    /// Electric Vehicle Supply Equipment
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class EVSEType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        /// <summary>Identified_ Object. MRID. Numeric_ Identifier
        /// urn:x-enexis:ecdm:uid:1:569198
        /// EVSE Identifier. This contains a number (&amp;gt; 0) designating an EVSE of the Charging Station.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.Always)]
        public int Id { get; set; }

        /// <summary>An id to designate a specific connector (on an EVSE) by connector index number.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("connectorId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int ConnectorId { get; set; }
    }

    /// <summary>Transaction
    /// urn:x-oca:ocpp:uid:2:233318
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class TransactionType
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        /// <summary>This contains the Id of the transaction.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("transactionId", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [System.ComponentModel.DataAnnotations.StringLength(36)]
        public string TransactionId { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingState", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public ChargingStateEnumType ChargingState { get; set; }

        /// <summary>Transaction. Time_ Spent_ Charging. Elapsed_ Time
        /// urn:x-oca:ocpp:uid:1:569415
        /// Contains the total time that energy flowed from EVSE to EV during the transaction (in seconds). Note that timeSpentCharging is smaller or equal to the duration of the transaction.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("timeSpentCharging", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int TimeSpentCharging { get; set; }

        [Newtonsoft.Json.JsonProperty("stoppedReason", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public ReasonEnumType StoppedReason { get; set; }

        /// <summary>The ID given to remote start request (&amp;lt;&amp;lt;requeststarttransactionrequest, RequestStartTransactionRequest&amp;gt;&amp;gt;. This enables to CSMS to match the started transaction to the given start request.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("remoteStartId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int RemoteStartId { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class TransactionEventRequest
    {
        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType CustomData { get; set; }

        [Newtonsoft.Json.JsonProperty("eventType", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public TransactionEventEnumType EventType { get; set; }

        [Newtonsoft.Json.JsonProperty("meterValue", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        public System.Collections.Generic.ICollection<MeterValueType> MeterValue { get; set; }

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
        public int SeqNo { get; set; }

        /// <summary>Indication that this transaction event happened when the Charging Station was offline. Default = false, meaning: the event occurred when the Charging Station was online.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("offline", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool Offline { get; set; } = false;

        /// <summary>If the Charging Station is able to report the number of phases used, then it SHALL provide it. When omitted the CSMS may be able to determine the number of phases used via device management.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("numberOfPhasesUsed", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int NumberOfPhasesUsed { get; set; }

        /// <summary>The maximum current of the connected cable in Ampere (A).
        /// </summary>
        [Newtonsoft.Json.JsonProperty("cableMaxCurrent", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int CableMaxCurrent { get; set; }

        /// <summary>This contains the Id of the reservation that terminates as a result of this transaction.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("reservationId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int ReservationId { get; set; }

        [Newtonsoft.Json.JsonProperty("transactionInfo", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required]
        public TransactionType TransactionInfo { get; set; } = new TransactionType();

        [Newtonsoft.Json.JsonProperty("evse", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public EVSEType Evse { get; set; }

        [Newtonsoft.Json.JsonProperty("idToken", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public IdTokenType IdToken { get; set; }
    }
}