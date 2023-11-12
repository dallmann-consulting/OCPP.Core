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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_OCPP20;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
    {
        public string HandleLogStatusNotification(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;

            Logger.LogTrace("Processing LogStatusNotification...");
            LogStatusNotificationResponse logStatusNotificationResponse = new LogStatusNotificationResponse();
            logStatusNotificationResponse.CustomData = new CustomDataType();
            logStatusNotificationResponse.CustomData.VendorId = VendorId;

            string status = null;

            try
            {
                LogStatusNotificationRequest logStatusNotificationRequest = DeserializeMessage<LogStatusNotificationRequest>(msgIn);
                Logger.LogTrace("LogStatusNotification => Message deserialized");


                if (ChargePointStatus != null)
                {
                    // Known charge station
                    status = logStatusNotificationRequest.Status.ToString();
                    Logger.LogInformation("LogStatusNotification => Status={0}", status);
                }
                else
                {
                    // Unknown charge station
                    errorCode = ErrorCodes.GenericError;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(logStatusNotificationResponse);
                Logger.LogTrace("LogStatusNotification => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "LogStatusNotification => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.InternalError;
            }

            WriteMessageLog(ChargePointStatus.Id, null, msgIn.Action, status, errorCode);
            return errorCode;
        }
    }
}
