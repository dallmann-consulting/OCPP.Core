using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages;

namespace OCPP.Core.Server
{
    public partial class Controller
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
        public Controller(IConfiguration config, ILoggerFactory loggerFactory, string chargePointIdentifier)
        {
            Configuration = config;
            Logger = loggerFactory.CreateLogger(typeof(Controller));

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
    }
}
