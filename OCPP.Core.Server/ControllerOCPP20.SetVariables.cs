/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2026 dallmann consulting GmbH.
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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_OCPP20;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
    {
        public void HandleSetVariables(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            Logger.LogInformation("SetVariables answer: ChargePointId={0} / MsgType={1} / ErrCode={2}", ChargePointStatus.Id, msgIn.MessageType, msgIn.ErrorCode);

            try
            {
                SetVariablesResponse setVariablesResponse = DeserializeMessage<SetVariablesResponse>(msgIn);
                string results = string.Join(", ", setVariablesResponse?.SetVariableResult?.Select(r => $"{r.Component?.Name}.{r.Variable?.Name}={r.AttributeStatus}") ?? Enumerable.Empty<string>());
                Logger.LogInformation("SetVariables => Answer: {0}", results);
                WriteMessageLog(ChargePointStatus?.Id, null, msgOut.Action, results, msgIn.ErrorCode);

                if (msgOut.TaskCompletionSource != null)
                {
                    // Return the OCPP response => mapped to the API format by the middleware
                    string apiResult = JsonConvert.SerializeObject(setVariablesResponse);
                    Logger.LogTrace("HandleSetVariables => API response: {0}", apiResult);

                    msgOut.TaskCompletionSource.SetResult(apiResult);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "HandleSetVariables => Exception: {0}", exp.Message);
            }
        }
    }
}
