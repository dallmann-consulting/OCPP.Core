﻿/*
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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages;

namespace OCPP.Core.Server
{
    public partial class Controller
    {
        public string HandleBootNotification(Message msgIn, Message msgOut)
        {
            string errorCode = null;

            try
            {
                Logger.LogTrace("Processing boot notification...");
                BootNotificationRequest bootNotificationRequest = JsonConvert.DeserializeObject<BootNotificationRequest>(msgIn.JsonPayload);
                Logger.LogTrace("BootNotification => Message deserialized");

                BootNotificationResponse bootNotificationResponse = new BootNotificationResponse();
                bootNotificationResponse.CurrentTime = DateTime.Now;
                bootNotificationResponse.Interval = 300;    // 300 seconds

                if (CurrentChargePoint != null)
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

            WriteMessageLog(CurrentChargePoint.ChargePointId, null, msgIn.Action, null, errorCode);
            return errorCode;
        }
    }
}
