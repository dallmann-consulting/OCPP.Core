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

#nullable disable

namespace OCPP.Core.Database
{
    /// <summary>
    /// Business event of a charge point (table "EventLog"): status changes, authorizations, transactions,
    /// messages of the charge point and commands sent to it. Not technical (=> MessageLog).
    /// The display text is created by the UI from EventCode and Parameters.
    /// </summary>
    public partial class ChargePointEvent
    {
        public long EventId { get; set; }

        /// <summary>
        /// Time of the event (UTC, received by the server)
        /// </summary>
        public DateTime EventTime { get; set; }

        public string ChargePointId { get; set; }
        public int? ConnectorId { get; set; }

        /// <summary>
        /// See <see cref="EventCategory"/>
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// See <see cref="EventSeverity"/>
        /// </summary>
        public string Severity { get; set; }

        /// <summary>
        /// See <see cref="Database.EventCode"/>
        /// </summary>
        public string EventCode { get; set; }

        /// <summary>
        /// Parameters of the event as JSON object (e.g. {"old":"Available","new":"Occupied"})
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Optional technical details
        /// </summary>
        public string Details { get; set; }

        public virtual ChargePoint ChargePoint { get; set; }
    }
}
