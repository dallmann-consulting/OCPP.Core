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

using System.Collections.Generic;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Models
{
    /// <summary>
    /// Events (EventLog) of one charge point
    /// </summary>
    public class ChargePointEventsViewModel
    {
        public const int PageSize = 100;

        public string ChargePointId { get; set; }
        public string ChargePointName { get; set; }

        /// <summary>
        /// Events of the current page (newest first)
        /// </summary>
        public List<ChargePointEvent> Events { get; set; } = new List<ChargePointEvent>();

        /// <summary>
        /// Connector names (key = connector ID)
        /// </summary>
        public Dictionary<int, string> ConnectorNames { get; set; } = new Dictionary<int, string>();

        /// <summary>
        /// Names of the tags in the events of the page (key = tag ID)
        /// </summary>
        public Dictionary<string, string> TagNames { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Time span: 1 = 30 days, 2 = 90 days, 3 = 365 days (as in the transaction list)
        /// </summary>
        public int Timespan { get; set; } = 1;

        /// <summary>
        /// Category filter (null = all)
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Minimum severity (null = all, "Warning" = warnings and errors, "Error" = only errors)
        /// </summary>
        public string Severity { get; set; }

        public int Page { get; set; } = 1;
        public int PageCount { get; set; } = 1;
        public int TotalCount { get; set; }
    }
}
