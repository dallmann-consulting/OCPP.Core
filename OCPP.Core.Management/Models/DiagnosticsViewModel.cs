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

namespace OCPP.Core.Management.Models
{
    public class DiagnosticsViewModel
    {
        public bool ServerOnline { get; set; }
        public List<ConnectorDiagnosticsRow> Connectors { get; set; } = new List<ConnectorDiagnosticsRow>();

        public int TotalConnectors => Connectors?.Count ?? 0;
        public int OnlineCount => Connectors?.Count(c => c.ChargePointOnline) ?? 0;
        public int OpenTransactionCount => Connectors?.Count(c => c.HasOpenTransaction) ?? 0;
        public int AnomalyCount => Connectors?.Count(c => c.IsAnomalous) ?? 0;
    }

    public class ConnectorDiagnosticsRow
    {
        public string ChargePointId { get; set; }
        public int ConnectorId { get; set; }
        public string ConnectorName { get; set; }

        // Live state from server API
        public bool ChargePointOnline { get; set; }
        public string OcppProtocol { get; set; }
        public ConnectorStatusEnum? LiveStatus { get; set; }
        public double? LiveChargeRateKW { get; set; }
        public double? LiveMeterKWH { get; set; }
        public double? LiveSoC { get; set; }

        // Persisted state from database
        public string DbStatus { get; set; }
        public DateTime? DbStatusTime { get; set; }
        public double? DbLastMeter { get; set; }
        public DateTime? DbLastMeterTime { get; set; }

        // Open transaction (StopTime == null)
        public bool HasOpenTransaction { get; set; }
        public int? OpenTransactionId { get; set; }
        public DateTime? OpenTransactionStart { get; set; }
        public string OpenTransactionTagId { get; set; }

        /// <summary>
        /// True when the connector state indicates a problem worth investigating:
        /// - Charge point offline but an open transaction exists in the DB
        /// - Live status is Available but an open transaction is recorded
        /// - Live status is Faulted
        /// </summary>
        public bool IsAnomalous =>
            (!ChargePointOnline && HasOpenTransaction) ||
            (ChargePointOnline && LiveStatus == ConnectorStatusEnum.Faulted) ||
            (ChargePointOnline && LiveStatus == ConnectorStatusEnum.Available && HasOpenTransaction);
    }
}
