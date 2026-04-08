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

using OCPP.Core.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Models
{
    public class ConnectorStatusViewModel
    {
        /// <summary>
        /// List of ConnectorStatuses from DB
        /// </summary>
        public List<ConnectorStatus> ConnectorStatuses { get; set; }

        /// <summary>
        /// Dictionary of online ConnectorStatuses for online/offline status
        /// </summary>
        public Dictionary<string, ChargePointStatus> OnlineConnectorStatuses { get; set; }

        /// <summary>
        /// List of charge tags for starting a remote transaction
        /// </summary>
        public List<ChargeTag> ChargeTags { get; set; }

        public string ChargePointId { get; set; }

        public int ConnectorId { get; set; }

        public string LastStatus { get; set; }

        public DateTime? LastStatusTime { get; set; }

        public double? LastMeter { get; set; }

        public DateTime? LastMeterTime { get; set; }

        [StringLength(100)]
        public string ConnectorName { get; set; }

    }
}
