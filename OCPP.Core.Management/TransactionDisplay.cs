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

using Microsoft.Extensions.Localization;
using OCPP.Core.Database;

namespace OCPP.Core.Management
{
    /// <summary>
    /// Understandable display texts for the start/stop tag of a transaction
    /// (list, report and exports). The database keeps the original values.
    /// </summary>
    public static class TransactionDisplay
    {
        /// <summary>
        /// Start tag: tag name (or ID) - or a text for transactions without token / with a token that was not accepted
        /// </summary>
        public static string StartTag(string startTagId, string startTagName, string startResult, IStringLocalizer<SharedResource> localizer)
        {
            if (string.IsNullOrWhiteSpace(startTagId))
            {
                // transaction started without token (OCPP 2.x: cable plugged in first, never authorized)
                return localizer["TransactionNoAuthorization"];
            }

            string tag = string.IsNullOrEmpty(startTagName) ? startTagId : startTagName;
            if (TransactionStatus.IsRejectedStartResult(startResult))
            {
                // started with a token that the server didn't accept (e.g. authorized offline by the charge point)
                return string.Format(localizer["TransactionTagNotAuthorized"], tag);
            }
            return tag;
        }

        /// <summary>
        /// Stop tag: tag name (or ID) - or a text for transactions that were closed by the server
        /// </summary>
        public static string StopTag(string stopTagId, string stopTagName, string stopReason, IStringLocalizer<SharedResource> localizer)
        {
            if (stopReason == TransactionStatus.StopReasonSuperseded)
            {
                // stop message lost => closed when the next transaction started (stop time and meter are estimated)
                return localizer["TransactionClosedAutomatically"];
            }
            return string.IsNullOrEmpty(stopTagName) ? stopTagId : stopTagName;
        }

        /// <summary>
        /// True for transactions without token and without charged energy (e.g. cable plugged in, never authorized)
        /// => not relevant for the charge report
        /// </summary>
        public static bool IsEmptyTransactionWithoutTag(Transaction transaction)
        {
            double energy = (transaction.MeterStop ?? transaction.MeterStart) - transaction.MeterStart;
            return string.IsNullOrWhiteSpace(transaction.StartTagId) && energy <= 0;
        }
    }
}
