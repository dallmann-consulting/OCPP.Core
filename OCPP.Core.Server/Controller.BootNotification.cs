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
