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

#nullable disable

namespace OCPP.Core.Database
{
    public partial class ChargePoint
    {
        public ChargePoint()
        {
            Transactions = new HashSet<Transaction>();
        }

        public string ChargePointId { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ClientCertThumb { get; set; }

        /// <summary>
        /// Highest connector ID (OCPP 2.x: EVSE ID) the charge point may use. Messages for higher IDs
        /// create no connector (status) => a charge point cannot create any number of connectors.
        /// </summary>
        public int MaxConnectorId { get; set; } = 1;

        public virtual ICollection<Transaction> Transactions { get; set; }
    }
}
