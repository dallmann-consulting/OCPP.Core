using Microsoft.Extensions.Logging;
using OCPP.Core.Server.Messages_OCPP16;
using System;

namespace OCPP.Core.Server
{
	public partial class ControllerOCPP16
	{
		public void HandleGetConfiguration(OCPPMessage msgIn, OCPPMessage msgOut)
		{
			Logger.LogInformation("GetConfiguration answer: ChargePointId={0} / MsgType={1} / ErrCode={2}", ChargePointStatus.Id, msgIn.MessageType, msgIn.ErrorCode);

			try
			{
				var getConfigurationResponse = DeserializeMessage<GetConfigurationResponse>(msgIn);
				Logger.LogInformation("GetConfiguration => KeyCount: {0}", getConfigurationResponse.ConfigurationKey?.Count);
				WriteMessageLog(ChargePointStatus?.Id, null, msgOut.Action, getConfigurationResponse.ConfigurationKey?.ToString(), msgIn.ErrorCode);

				if(msgOut.TaskCompletionSource != null)
				{
					msgOut.TaskCompletionSource.SetResult(msgIn.JsonPayload);
				}
			}
			catch(Exception exp)
			{
				Logger.LogError(exp, "GetConfiguration => Exception: {0}", exp.Message);
			}
		}
	}
}
