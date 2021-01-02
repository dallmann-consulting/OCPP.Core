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
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        /// <summary>
        /// Configuration context for reading app settings
        /// </summary>
        private IConfiguration Configuration { get; set; }

        /// <summary>
        /// Chargepoint
        /// </summary>
        private ChargePoint CurrentChargePoint { get; set; }

        /// <summary>
        /// ILogger object
        /// </summary>
        private ILogger Logger { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ControllerOCPP16(IConfiguration config, ILoggerFactory loggerFactory, string chargePointIdentifier)
        {
            Configuration = config;
            Logger = loggerFactory.CreateLogger(typeof(ControllerOCPP16));

            if (!string.IsNullOrWhiteSpace(chargePointIdentifier))
            {
                using (OCPPCoreContext dbContext = new OCPPCoreContext(Configuration))
                {
                    CurrentChargePoint = dbContext.Find<ChargePoint>(chargePointIdentifier);
                    if (CurrentChargePoint != null)
                    {
                        Logger.LogInformation("New Controller => Found chargepoint with identifier={0}", CurrentChargePoint.ChargePointId);
                    }
                    else
                    {
                        Logger.LogWarning("New Controller => Found no chargepoint with identifier={0}", chargePointIdentifier);
                    }
                }
            }
            else
            {
                CurrentChargePoint = null;
                Logger.LogWarning("New Controller => empty chargepoint identifier");
            }
        }

        /// <summary>
        /// Processes the charge point message and returns the answer message
        /// </summary>
        public Message ProcessMessage(Message msgIn)
        {
            Message msgOut = new Message();
            msgOut.MessageType = "3";
            msgOut.UniqueId = msgIn.UniqueId;

            string errorCode = null;

            if (msgIn.MessageType == "2")
            {
                switch (msgIn.Action)
                {
                    case "BootNotification":
                        errorCode = HandleBootNotification(msgIn, msgOut);
                        break;

                    case "Heartbeat":
                        errorCode = HandleHeartBeat(msgIn, msgOut);
                        break;

                    case "Authorize":
                        errorCode = HandleAuthorize(msgIn, msgOut);
                        break;

                    case "StartTransaction":
                        errorCode = HandleStartTransaction(msgIn, msgOut);
                        break;

                    case "StopTransaction":
                        errorCode = HandleStopTransaction(msgIn, msgOut);
                        break;

                    case "MeterValues":
                        errorCode = HandleMeterValues(msgIn, msgOut);
                        break;

                    case "StatusNotification":
                        errorCode = HandleStatusNotification(msgIn, msgOut);
                        break;

                    case "DataTransfer":
                        errorCode = HandleDataTransfer(msgIn, msgOut);
                        break;

                    default:
                        errorCode = ErrorCodes.NotSupported;
                        WriteMessageLog(CurrentChargePoint.ChargePointId, null, msgIn.Action, msgIn.JsonPayload, errorCode);
                        break;
                }
            }
            else
            {
                Logger.LogError("Protocol error => wrong message type", msgIn.MessageType);
                errorCode = ErrorCodes.ProtocolError;
            }

            if (!string.IsNullOrEmpty(errorCode))
            {
                // Inavlid message type => return type "4" (CALLERROR)
                msgOut.MessageType = "4";
                msgOut.ErrorCode = errorCode;
                Logger.LogDebug("Return error code messge: ErrorCode={0}", errorCode);
            }

            return msgOut;
        }

        /// <summary>
        /// Helper function for writing a log entry in database
        /// </summary>
        private bool WriteMessageLog(string chargePointId, int? connectorId, string message, string result, string errorCode)
        {
            try
            {
                bool dbMessageLog = Configuration.GetValue<bool>("DbMessageLog", false);
                if (dbMessageLog)
                {
                    if (!string.IsNullOrWhiteSpace(chargePointId))
                    {
                        using (OCPPCoreContext dbContext = new OCPPCoreContext(Configuration))
                        {
                            MessageLog msgLog = new MessageLog();
                            msgLog.ChargePointId = chargePointId;
                            msgLog.ConnectorId = connectorId;
                            msgLog.LogTime = DateTime.Now;
                            msgLog.Message = message;
                            msgLog.Result = result;
                            msgLog.ErrorCode = errorCode;
                            dbContext.MessageLogs.Add(msgLog);
                            Logger.LogTrace("MessageLog => Writing entry '{0}'", message);
                            dbContext.SaveChanges();
                        }
                        return true;
                    }
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "MessageLog => Error writing entry '{0}'", message);
            }
            return false;
        }
    }
}
