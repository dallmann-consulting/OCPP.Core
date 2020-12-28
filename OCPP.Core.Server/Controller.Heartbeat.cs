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
        public string HandleHeartBeat(Message msgIn, Message msgOut)
        {
            string errorCode = null;

            Logger.LogTrace("Processing heartbeat...");
            HeartbeatResponse heartbeatResponse = new HeartbeatResponse();
            heartbeatResponse.CurrentTime = DateTime.Now;

            msgOut.JsonPayload = JsonConvert.SerializeObject(heartbeatResponse);
            Logger.LogTrace("Heartbeat => Response serialized");

            WriteMessageLog(CurrentChargePoint?.ChargePointId, null, msgIn.Action, null, errorCode);
            return errorCode;
        }
    }
}
