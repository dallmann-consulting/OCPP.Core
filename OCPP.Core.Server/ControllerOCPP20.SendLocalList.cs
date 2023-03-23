using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Server.Messages_OCPP20;
using System;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
    {
        public void HandleSendLocalList(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            Logger.LogInformation("SendLocalList answer: ChargePointId={0} / MsgType={1} / ErrCode={2}",
                ChargePointStatus.Id, msgIn.MessageType, msgIn.ErrorCode);

            try
            {
                SendLocalListResponse response = JsonConvert.DeserializeObject<SendLocalListResponse>(msgIn.JsonPayload);
                Logger.LogInformation("HandleSendLocalList => Answer status: {0}", response?.Status);
                WriteMessageLog(ChargePointStatus?.Id, null, msgOut.Action, response?.Status.ToString(), msgIn.ErrorCode);

                if (msgOut.TaskCompletionSource != null)
                {
                    // set API response as TaskCompletion result
                    string apiResult = "{\"status\": " + JsonConvert.ToString(response?.Status.ToString()) + "}";
                    Logger.LogTrace("HandleSendLocalList => API response: {0}", apiResult);

                    msgOut.TaskCompletionSource.SetResult(apiResult);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "HandleSendLocalList => Exception: {0}", exp.Message);
            }
        }
    }
}
