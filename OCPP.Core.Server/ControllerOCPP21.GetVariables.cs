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
using OCPP.Core.Server.Messages_OCPP21;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP21
    {
        public void HandleGetVariables(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            Logger.LogInformation("GetVariables answer: ChargePointId={0} / MsgType={1} / ErrCode={2}", ChargePointStatus.Id, msgIn.MessageType, msgIn.ErrorCode);

            try
            {
                GetVariablesResponse getVariablesResponse = DeserializeMessage<GetVariablesResponse>(msgIn);
                Logger.LogInformation("GetVariables => Answer with {0} results", getVariablesResponse?.GetVariableResult?.Count);
                WriteMessageLog(ChargePointStatus?.Id, null, msgOut.Action, "GetVariables response", msgIn.ErrorCode);

                if (msgOut.TaskCompletionSource != null)
                {
                    // Return the OCPP response => mapped to the API format by the middleware
                    string apiResult = JsonConvert.SerializeObject(getVariablesResponse);
                    Logger.LogTrace("HandleGetVariables => API response: {0}", apiResult);

                    msgOut.TaskCompletionSource.SetResult(apiResult);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "HandleGetVariables => Exception: {0}", exp.Message);
            }
        }
    }
}
