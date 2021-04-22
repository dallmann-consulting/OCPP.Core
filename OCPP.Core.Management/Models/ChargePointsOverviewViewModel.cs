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

namespace OCPP.Core.Management.Models
{
    public class ChargePointsOverviewViewModel
    {
        /// <summary>
        /// ID of this chargepoint
        /// </summary>
        public string ChargePointId { get; set; }

        /// <summary>
        /// Connector-ID
        /// </summary>
        public int ConnectorId { get; set; }

        /// <summary>
        /// Name of this chargepoint
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Comment of this chargepoint
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// Meter start value of last transaction
        /// </summary>
        public double MeterStart { get; set; }

        /// <summary>
        /// Meter stop value of last transaction (or null if charging)
        /// </summary>
        public double? MeterStop { get; set; }

        /// <summary>
        /// Start time of last transaction
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Stop time of last transaction (or null if charging)
        /// </summary>
        public DateTime? StopTime { get; set; }

        /// <summary>
        /// Status of chargepoint
        /// </summary>
        public ConnectorStatusEnum ConnectorStatus { get; set; }

        /// <summary>
        /// Is this chargepoint currently connected to OCPP.Server?
        /// </summary>
        public bool Online { get; set; }

        /// <summary>
        /// Details about the current charge process
        /// </summary>
        public string CurrentChargeData { get; set; }
    }
}
