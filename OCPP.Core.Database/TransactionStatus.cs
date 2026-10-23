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

namespace OCPP.Core.Database
{
    /// <summary>
    /// Values in the transaction table with a special meaning (shared by server and management)
    /// </summary>
    public static class TransactionStatus
    {
        /// <summary>
        /// StopReason of an open transaction that was closed by the server
        /// because a new transaction started on the same connector (stop message lost)
        /// </summary>
        public const string StopReasonSuperseded = "Superseded";

        /// <summary>
        /// Authorization results (OCPP 1.6 + 2.x) that mean "token not accepted"
        /// </summary>
        private static readonly HashSet<string> RejectedStartResults = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Blocked",
            "Expired",
            "Invalid",
            "Unknown",
            "ConcurrentTx",
            "NoCredit",
            "NotAllowedTypeEVSE",
            "NotAtThisLocation",
            "NotAtThisTime"
        };

        /// <summary>
        /// True if the transaction was started with a token that the server didn't accept
        /// (e.g. authorized offline by the charge point).
        /// Other values (e.g. old OCPP 2.x entries with the trigger reason) are not rejected.
        /// </summary>
        public static bool IsRejectedStartResult(string startResult)
        {
            return !string.IsNullOrEmpty(startResult) && RejectedStartResults.Contains(startResult);
        }
    }
}
