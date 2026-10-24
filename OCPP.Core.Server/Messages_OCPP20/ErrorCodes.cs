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
using System.Threading.Tasks;

namespace OCPP.Core.Server.Messages_OCPP20
{
    /// <summary>
    /// Defined OCPP-J error codes (OCPP-J 2.0.1/2.1 part 4 "RPC Framework Error Codes")
    /// </summary>
    public class ErrorCodes
    {
        /// <summary>
        /// Payload for Action is syntactically incorrect
        /// </summary>
        public static string FormatViolation = "FormatViolation";

        /// <summary>
        /// Any other error not covered by the more specific error codes
        /// </summary>
        public static string GenericError = "GenericError";

        /// <summary>
        /// An internal error occurred and the receiver was not able to process the requested Action successfully
        /// </summary>
        public static string InternalError = "InternalError";

        /// <summary>
        /// A message with a Message Type Number received that is not supported by this implementation
        /// </summary>
        public static string MessageTypeNotSupported = "MessageTypeNotSupported";

        /// <summary>
        /// Requested Action is not known by receiver
        /// </summary>
        public static string NotImplemented = "NotImplemented";

        /// <summary>
        /// Requested Action is recognized but not supported by the receiver
        /// </summary>
        public static string NotSupported = "NotSupported";

        /// <summary>
        /// Payload for Action is syntactically correct but at least one of the fields violates occurrence constraints
        /// </summary>
        public static string OccurrenceConstraintViolation = "OccurrenceConstraintViolation";

        /// <summary>
        /// Payload is syntactically correct but at least one field contains an invalid value
        /// </summary>
        public static string PropertyConstraintViolation = "PropertyConstraintViolation";

        /// <summary>
        /// Payload for Action is not conform the PDU structure
        /// </summary>
        public static string ProtocolError = "ProtocolError";

        /// <summary>
        /// Content of the call is not a valid RPC request, e.g. the message ID could not be read
        /// </summary>
        public static string RpcFrameworkError = "RpcFrameworkError";

        /// <summary>
        /// During the processing of Action a security issue occurred preventing receiver from completing the Action successfully
        /// </summary>
        public static string SecurityError = "SecurityError";

        /// <summary>
        /// Payload for Action is syntactically correct but at least one of the fields violates data type constraints (e.g. "somestring": 12)
        /// </summary>
        public static string TypeConstraintViolation = "TypeConstraintViolation";
    }
}
