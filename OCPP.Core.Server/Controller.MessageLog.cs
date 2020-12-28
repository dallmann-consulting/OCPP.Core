using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;

namespace OCPP.Core.Server
{
    public partial class Controller
    {
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
            catch(Exception exp)
            {
                Logger.LogError(exp, "MessageLog => Error writing entry '{0}'", message);
            }
            return false;
        }
    }
}
