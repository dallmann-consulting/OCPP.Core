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

    /// <summary>Cost, energy, time or SoC limit for a transaction.
    /// </summary>
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class TransactionLimitType
    {
        /// <summary>Maximum allowed cost of transaction in currency of tariff.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("maxCost", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? MaxCost { get; set; }

        /// <summary>Maximum allowed energy in Wh to charge in transaction.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("maxEnergy", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? MaxEnergy { get; set; }

        /// <summary>Maximum duration of transaction in seconds from start to end.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("maxTime", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? MaxTime { get; set; }

        /// <summary>Maximum State of Charge of EV in percentage.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("maxSoC", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0, 100)]
        public int? MaxSoC { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }


    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class TransactionEventResponse
    {
        /// <summary>When _eventType_ of TransactionEventRequest is Updated, then this value contains the running cost. When _eventType_ of TransactionEventRequest is Ended, then this contains the final total cost of this transaction, including taxes, in the currency configured with the Configuration Variable: Currency. Absence of this value does not imply that the transaction was free. To indicate a free transaction, the CSMS SHALL send a value of 0.00.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("totalCost", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double? TotalCost { get; set; }

        /// <summary>Priority from a business point of view. Default priority is 0, The range is from -9 to 9. Higher values indicate a higher priority. The chargingPriority in &amp;lt;&amp;lt;transactioneventresponse,TransactionEventResponse&amp;gt;&amp;gt; is temporarily, so it may not be set in the &amp;lt;&amp;lt;cmn_idtokeninfotype,IdTokenInfoType&amp;gt;&amp;gt; afterwards. Also the chargingPriority in &amp;lt;&amp;lt;transactioneventresponse,TransactionEventResponse&amp;gt;&amp;gt; has a higher priority than the one in &amp;lt;&amp;lt;cmn_idtokeninfotype,IdTokenInfoType&amp;gt;&amp;gt;.  
        /// </summary>
        [Newtonsoft.Json.JsonProperty("chargingPriority", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? ChargingPriority { get; set; }

        [Newtonsoft.Json.JsonProperty("idTokenInfo", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public IdTokenInfoType? IdTokenInfo { get; set; }

        [Newtonsoft.Json.JsonProperty("transactionLimit", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public TransactionLimitType? TransactionLimit { get; set; }

        [Newtonsoft.Json.JsonProperty("updatedPersonalMessage", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public MessageContentType? UpdatedPersonalMessage { get; set; }

        [Newtonsoft.Json.JsonProperty("updatedPersonalMessageExtra", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.MinLength(1)]
        [System.ComponentModel.DataAnnotations.MaxLength(4)]
        public System.Collections.Generic.ICollection<MessageContentType>? UpdatedPersonalMessageExtra { get; set; }

        [Newtonsoft.Json.JsonProperty("customData", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public CustomDataType? CustomData { get; set; }
    }
}