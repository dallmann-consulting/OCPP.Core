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

namespace OCPP.Core.Server
{
    /// <summary>
    /// OCPP-J message types (MessageTypeId).
    /// Constants (no enum) because the message type is a string in the public extension interface IOCPPMessage.
    /// </summary>
    public static class OcppMessageType
    {
        /// <summary>
        /// Request
        /// </summary>
        public const string Call = "2";

        /// <summary>
        /// Response to a request
        /// </summary>
        public const string CallResult = "3";

        /// <summary>
        /// Error response to a request
        /// </summary>
        public const string CallError = "4";

        /// <summary>
        /// (OCPP 2.1) Error response to a CallResult
        /// </summary>
        public const string CallResultError = "5";

        /// <summary>
        /// (OCPP 2.1) Unconfirmed message without response
        /// </summary>
        public const string Send = "6";
    }
}
