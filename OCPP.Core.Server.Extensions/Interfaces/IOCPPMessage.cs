/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2024 dallmann consulting GmbH.
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

namespace OCPP.Core.Server.Extensions.Interfaces
{
    public interface IOCPPMessage
    {
        /// <summary>
        /// Message type
        /// </summary>
        string MessageType { get; set; }

        /// <summary>
        /// Message ID
        /// </summary>
        string UniqueId { get; set; }

        /// <summary>
        /// Action
        /// </summary>
        string Action { get; set; }

        /// <summary>
        /// JSON-Payload
        /// </summary>
        string JsonPayload { get; set; }
    }
}
