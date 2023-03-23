using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Server.Messages_OCPP20;
using System;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
    {
        public void HandleGetLocalListVersion(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            Logger.LogInformation("GetLocalListVersion answer: ChargePointId={0} / MsgType={1} / ErrCode={2}",
                ChargePointStatus.Id, msgIn.MessageType, msgIn.ErrorCode);

            try
            {
                GetLocalListVersionResponse response = JsonConvert.DeserializeObject<GetLocalListVersionResponse>(msgIn.JsonPayload);
                Logger.LogInformation("HandleGetLocalListVersion => Answer status: {0}", response?.VersionNumber);
                WriteMessageLog(ChargePointStatus?.Id, null, msgOut.Action, response?.VersionNumber.ToString(), msgIn.ErrorCode);

                if (msgOut.TaskCompletionSource != null)
                {
                    // set API response as TaskCompletion result
                    string apiResult = "{\"versionNumber\": " + JsonConvert.ToString(response?.VersionNumber.ToString()) + "}";
                    Logger.LogTrace("HandleGetLocalListVersion => API response: {0}", apiResult);

                    msgOut.TaskCompletionSource.SetResult(apiResult);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "HandleGetLocalListVersion => Exception: {0}", exp.Message);
            }
        }
    }
}
