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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public string HandleBootNotification(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;

            try
            {
                Logger.LogTrace("Processing boot notification...");
                BootNotificationRequest bootNotificationRequest = DeserializeMessage<BootNotificationRequest>(msgIn);
                Logger.LogTrace("BootNotification => Message deserialized");

                BootNotificationResponse bootNotificationResponse = new BootNotificationResponse();
                bootNotificationResponse.CurrentTime = DateTimeOffset.UtcNow;
                bootNotificationResponse.Interval = Configuration.GetValue<int>("HeartBeatInterval", 300);  // in seconds

                if (ChargePointStatus != null)
                {
                    // Known charge station => accept
                    bootNotificationResponse.Status = BootNotificationResponseStatus.Accepted;
                }
                else
                {
                    // Unknown charge station => reject
                    bootNotificationResponse.Status = BootNotificationResponseStatus.Rejected;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(bootNotificationResponse);
                Logger.LogTrace("BootNotification => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "BootNotification => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.FormationViolation;
            }

            WriteMessageLog(ChargePointStatus.Id, null, msgIn.Action, null, errorCode);
            return errorCode;
        }
    }
}
