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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace OCPP.Core.Server
{
    /// <summary>
    /// Live status of the connectors after a (re)connect: every connection starts with an empty status object,
    /// but charge points send a StatusNotification only after a boot or a change of the status.
    /// </summary>
    public partial class OCPPMiddleware
    {
        /// <summary>
        /// Sets the last known status of all connectors (database) as live status of a new connection
        /// until the chargepoint reports the current status. Returns the IDs of the known connectors.
        /// Must be called before the receive loop starts (=> doesn't overwrite newer reported values).
        /// </summary>
        private List<int> LoadConnectorStatus(ChargePointStatus chargePointStatus, OCPPCoreContext dbContext)
        {
            List<int> connectorIds = new List<int>();
            try
            {
                List<ConnectorStatus> dbConnectors = dbContext.ConnectorStatuses
                    .Where(cs => cs.ChargePointId == chargePointStatus.Id && cs.ConnectorId > 0)
                    .AsNoTracking()
                    .ToList();

                foreach (ConnectorStatus cs in dbConnectors)
                {
                    connectorIds.Add(cs.ConnectorId);

                    // only the status - no meter values: the power is calculated from the difference to the previous meter value
                    if (TryParseConnectorStatus(cs.LastStatus, out ConnectorStatusEnum status))
                    {
                        chargePointStatus.OnlineConnectors.TryAdd(cs.ConnectorId, new OnlineConnectorStatus() { Status = status });
                    }
                }
                _logger.LogDebug("OCPPMiddleware => Last known status of {0} connector(s) loaded for chargepoint '{1}'", chargePointStatus.OnlineConnectors.Count, chargePointStatus.Id);
            }
            catch (Exception exp)
            {
                _logger.LogError(exp, "OCPPMiddleware => Error loading connector status of chargepoint '{0}': {1}", chargePointStatus.Id, exp.Message);
            }
            connectorIds.Sort();
            return connectorIds;
        }

        /// <summary>
        /// Status text in the database (name of ConnectorStatusEnum) => enum value ("Undefined" and unknown texts => false)
        /// </summary>
        private static bool TryParseConnectorStatus(string text, out ConnectorStatusEnum status)
        {
            status = ConnectorStatusEnum.Undefined;
            return !string.IsNullOrWhiteSpace(text) && !char.IsDigit(text.Trim()[0]) &&
                   Enum.TryParse(text.Trim(), ignoreCase: true, out status) && Enum.IsDefined(status) &&
                   status != ConnectorStatusEnum.Undefined;
        }

        /// <summary>
        /// Requests the current status of the known connectors with TriggerMessage(StatusNotification).
        /// Waits "TriggerStatusAfterConnect" seconds first: after a boot the chargepoint sends the status by itself
        /// (and OCPP doesn't allow requests of the server before the BootNotification) => no trigger then.
        /// Stops at the first connector that isn't accepted (e.g. "NotImplemented" => the chargepoint doesn't support it).
        /// </summary>
        private async Task TriggerStatusNotificationsAsync(ChargePointStatus chargePointStatus, IReadOnlyList<int> connectorIds)
        {
            int delaySeconds = _configuration.GetValue<int>("TriggerStatusAfterConnect", 5);
            if (delaySeconds <= 0 || connectorIds.Count == 0) return;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                if (chargePointStatus.BootNotificationReceived)
                {
                    _logger.LogDebug("OCPPMiddleware => Chargepoint '{0}' has booted => no TriggerMessage for the status", chargePointStatus.Id);
                    return;
                }

                foreach (int connectorId in connectorIds)
                {
                    if (chargePointStatus.WebSocket?.State != WebSocketState.Open) return;

                    OcppRequestResult result;
                    switch (chargePointStatus.Protocol)
                    {
                        case Protocol_OCPP21:
                            result = await TriggerStatusNotification21(chargePointStatus, connectorId);
                            break;
                        case Protocol_OCPP201:
                            result = await TriggerStatusNotification20(chargePointStatus, connectorId);
                            break;
                        default:
                            result = await TriggerStatusNotification16(chargePointStatus, connectorId);
                            break;
                    }

                    string status = result.NoAnswerStatus;
                    if (status == null)
                    {
                        try
                        {
                            status = JToken.Parse(result.Answer)["status"]?.Value<string>();
                        }
                        catch (JsonException)
                        {
                        }
                    }
                    if (!string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("OCPPMiddleware => TriggerMessage(StatusNotification) for chargepoint '{0}' / connector {1} not accepted: {2} {3} => no further triggers",
                            chargePointStatus.Id, connectorId, status, result.ErrorInfo);
                        return;
                    }
                }
                _logger.LogInformation("OCPPMiddleware => Status of {0} connector(s) requested from chargepoint '{1}'", connectorIds.Count, chargePointStatus.Id);
            }
            catch (Exception exp)
            {
                _logger.LogError(exp, "OCPPMiddleware => Error requesting the connector status of chargepoint '{0}': {1}", chargePointStatus.Id, exp.Message);
            }
        }
    }
}
