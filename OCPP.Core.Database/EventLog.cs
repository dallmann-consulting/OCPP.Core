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
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OCPP.Core.Database
{
    /// <summary>
    /// Categories of the events (EventLog)
    /// </summary>
    public static class EventCategory
    {
        public const string Connection = "Connection";
        public const string Station = "Station";
        public const string Status = "Status";
        public const string Authorization = "Authorization";
        public const string Transaction = "Transaction";
        public const string Command = "Command";

        public static readonly string[] All = { Connection, Station, Status, Authorization, Transaction, Command };
    }

    /// <summary>
    /// Severities of the events (EventLog)
    /// </summary>
    public static class EventSeverity
    {
        public const string Info = "Info";
        public const string Warning = "Warning";
        public const string Error = "Error";

        public static readonly string[] All = { Info, Warning, Error };
    }

    /// <summary>
    /// Event codes (EventLog). The parameters of each event are noted in the comment.
    /// </summary>
    public static class EventCode
    {
        // Connection
        /// <summary>Parameters: protocol</summary>
        public const string Connected = "Connected";
        /// <summary>Parameters: closeStatus</summary>
        public const string Disconnected = "Disconnected";
        public const string ConnectionReplaced = "ConnectionReplaced";
        /// <summary>Parameters: reason</summary>
        public const string ConnectionRefused = "ConnectionRefused";
        /// <summary>Parameters: method (Password, Certificate, Missing, NotConfigured)</summary>
        public const string AuthenticationFailed = "AuthenticationFailed";

        // Station
        /// <summary>Parameters: vendor, model, firmware, reason</summary>
        public const string Boot = "Boot";
        /// <summary>Parameters: old, new</summary>
        public const string FirmwareChanged = "FirmwareChanged";

        // Status
        /// <summary>Parameters: old, new, errorCode, info</summary>
        public const string StatusChanged = "StatusChanged";

        // Authorization
        /// <summary>Parameters: tag, action (Authorize, Start, Stop)</summary>
        public const string TokenAccepted = "TokenAccepted";
        /// <summary>Parameters: tag, status, action</summary>
        public const string TokenRejected = "TokenRejected";

        // Transaction
        /// <summary>Parameters: transactionId, tag, meterStart</summary>
        public const string TransactionStarted = "TransactionStarted";
        /// <summary>Parameters: transactionId, tag, energy, reason</summary>
        public const string TransactionStopped = "TransactionStopped";
        /// <summary>Parameters: transactionId, energy</summary>
        public const string TransactionClosedAutomatically = "TransactionClosedAutomatically";
        /// <summary>Parameters: transactionId, startTag, stopTag</summary>
        public const string StopTagNotPermitted = "StopTagNotPermitted";
        /// <summary>Parameters: transactionId, start, last, new, takenOver</summary>
        public const string MeterDecreased = "MeterDecreased";

        // Command (server => charge point)
        /// <summary>Parameters: action, result, user and the parameters of the command (e.g. connector, tag, type)</summary>
        public const string Command = "Command";
    }

    /// <summary>
    /// Writes and reads events of the EventLog
    /// </summary>
    public static class EventLogExtensions
    {
        /// <summary>
        /// Writes an event immediately (independent of other pending changes of the context).
        /// Errors are only logged: the processing of OCPP messages must never fail because of the EventLog.
        /// </summary>
        public static void WriteEvent(this OCPPCoreContext dbContext, ILogger logger, string chargePointId, int? connectorId,
            string category, string severity, string eventCode, IDictionary<string, object> parameters = null, string details = null)
        {
            if (dbContext == null || string.IsNullOrEmpty(chargePointId)) return;

            try
            {
                DateTime eventTime = DateTime.UtcNow;
                string json = (parameters != null && parameters.Count > 0) ? JsonSerializer.Serialize(parameters) : null;
                // direct INSERT (not via the change tracker) => doesn't save other pending changes of the caller
                dbContext.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO EventLog (EventTime, ChargePointId, ConnectorId, Category, Severity, EventCode, Parameters, Details) VALUES ({eventTime}, {chargePointId}, {connectorId}, {category}, {severity}, {eventCode}, {json}, {details})");
            }
            catch (Exception exp)
            {
                logger?.LogError(exp, "EventLog => Error writing event '{0}' of chargepoint '{1}': {2}", eventCode, chargePointId, exp.Message);
            }
        }

        /// <summary>
        /// Parameters of an event (JSON) as dictionary (empty for no or invalid JSON)
        /// </summary>
        public static Dictionary<string, string> ReadParameters(string parameters)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(parameters)) return result;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(parameters);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
                foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    result[p.Name] = p.Value.ValueKind switch
                    {
                        JsonValueKind.String => p.Value.GetString(),
                        JsonValueKind.Null => null,
                        _ => p.Value.GetRawText()
                    };
                }
            }
            catch (JsonException)
            {
            }
            return result;
        }
    }
}
