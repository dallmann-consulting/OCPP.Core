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

namespace OCPP.Core.Server.Messages_OCPP16
{
    /// <summary>
    /// Defined OCPP error codes
    /// </summary>
    public class ErrorCodes
    {
        /// <summary>
        /// Requested Action is recognized but not supported by the receiver
        /// </summary>
        public static string NotSupported = "NotSupported";

        /// <summary>
        /// InternalError An internal error occurred and the receiver was not able to process the requested Action successfully
        /// </summary>
        public static string InternalError = "InternalError";

        /// <summary>
        /// Payload for Action is incomplete
        /// </summary>
        public static string ProtocolError = "ProtocolError";

        /// <summary>
        /// During the processing of Action a security issue occurred preventing receiver from completing the Action successfully
        /// </summary>
        public static string SecurityError = "SecurityError";

        /// <summary>
        /// Payload for Action is syntactically incorrect or not conform the PDU structure for Action
        /// </summary>
        public static string FormationViolation = "FormationViolation";

        /// <summary>
        /// Payload is syntactically correct but at least one field contains an invalid value
        /// </summary>
        public static string PropertyConstraintViolation = "PropertyConstraintViolation";

        /// <summary>
        /// Payload for Action is syntactically correct but at least one of the fields violates occurence constraints
        /// </summary>
        public static string OccurenceConstraintViolation = "OccurenceConstraintViolation";

        /// <summary>
        ///  Payload for Action is syntactically correct but at least one of the fields violates data type constraints(e.g. “somestring”: 12)
        /// </summary>
        public static string TypeConstraintViolation = "TypeConstraintViolation";

        /// <summary>
        /// Any other error not covered by the previous ones
        /// </summary>
        public static string GenericError = "GenericError";
    }
}
