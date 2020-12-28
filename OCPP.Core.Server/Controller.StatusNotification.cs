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
        public string HandleStatusNotification(Message msgIn, Message msgOut)
        {
            string errorCode = null;
            StatusNotificationResponse statusNotificationResponse = new StatusNotificationResponse();

            int? connectorId = null;
            bool msgWritten = false;

            try
            {
                Logger.LogTrace("Processing status notification...");
                StatusNotificationRequest statusNotificationRequest = JsonConvert.DeserializeObject<StatusNotificationRequest>(msgIn.JsonPayload);
                Logger.LogTrace("StatusNotification => Message deserialized");

                connectorId = statusNotificationRequest.ConnectorId;

                if (CurrentChargePoint != null)
                {
                    // Known charge station
                    msgWritten = WriteMessageLog(CurrentChargePoint.ChargePointId, connectorId, msgIn.Action, string.Format("Info={0} / Status={1} / ", statusNotificationRequest.Info, statusNotificationRequest.Status), statusNotificationRequest.ErrorCode.ToString());
                }
                else
                {
                    // Unknown charge station
                    errorCode = ErrorCodes.GenericError;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(statusNotificationResponse);
                Logger.LogTrace("StatusNotification => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "StatusNotification => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.InternalError;
            }

            if (!msgWritten)
            {
                WriteMessageLog(CurrentChargePoint.ChargePointId, connectorId, msgIn.Action, null, errorCode);
            }
            return errorCode;
        }
    }
}
