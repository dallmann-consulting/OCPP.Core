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
using System.Net.WebSockets;
using Newtonsoft.Json;
using OCPP.Core.Database;

namespace OCPP.Core.Server
{
    /// <summary>
    /// Encapsulates the data of a connected chargepoint in the server
    /// </summary>
    public class ChargePointStatus
    {
        private Dictionary<int, OnlineConnectorStatus> _onlineConnectors;

        public ChargePointStatus()
        {
        }

        public ChargePointStatus(ChargePoint chargePoint)
        {
            Id = chargePoint.ChargePointId;
            Name = chargePoint.Name;
        }

        /// <summary>
        /// ID of chargepoint
        /// </summary>
        [Newtonsoft.Json.JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// Name of chargepoint
        /// </summary>
        [Newtonsoft.Json.JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// OCPP protocol version
        /// </summary>
        [Newtonsoft.Json.JsonProperty("protocol")]
        public string Protocol { get; set; }

        /// <summary>
        /// Dictionary with online connectors
        /// </summary>
        public Dictionary<int, OnlineConnectorStatus> OnlineConnectors
        {
            get
            {
                if (_onlineConnectors == null)
                {
                    _onlineConnectors = new Dictionary<int, OnlineConnectorStatus>();
                }
                return _onlineConnectors;
            }
            set
            {
                _onlineConnectors = value;
            }
        }

        /// <summary>
        /// WebSocket for internal processing
        /// </summary>
        [JsonIgnore]
        public WebSocket WebSocket { get; set; }
    }

    /// <summary>
    /// Encapsulates details about online charge point connectors
    /// </summary>
    public class OnlineConnectorStatus
    {
        /// <summary>
        /// Status of charge connector
        /// </summary>
        public ConnectorStatusEnum Status { get; set; }

        /// <summary>
        /// Current charge rate in kW
        /// </summary>
        public double? ChargeRateKW { get; set; }

        /// <summary>
        /// Current meter value in kWh
        /// </summary>
        public double? MeterKWH { get; set; }

        /// <summary>
        /// StateOfCharges in percent
        /// </summary>
        public double? SoC { get; set; }
    }

    public enum ConnectorStatusEnum
    {
        [System.Runtime.Serialization.EnumMember(Value = @"")]
        Undefined = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Available")]
        Available = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"Occupied")]
        Occupied = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"Unavailable")]
        Unavailable = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"Faulted")]
        Faulted = 4
    }
}
