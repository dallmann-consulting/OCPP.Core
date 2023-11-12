/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_OCPP20;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
    {
        public string HandleClearedChargingLimit(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;

            Logger.LogTrace("Processing ClearedChargingLimit...");
            ClearedChargingLimitResponse clearedChargingLimitResponse = new ClearedChargingLimitResponse();
            clearedChargingLimitResponse.CustomData = new CustomDataType();
            clearedChargingLimitResponse.CustomData.VendorId = VendorId;

            string source = null;
            int connectorId = 0;

            try
            {
                ClearedChargingLimitRequest clearedChargingLimitRequest = DeserializeMessage<ClearedChargingLimitRequest>(msgIn);
                Logger.LogTrace("ClearedChargingLimit => Message deserialized");

                if (ChargePointStatus != null)
                {
                    // Known charge station
                    source = clearedChargingLimitRequest.ChargingLimitSource.ToString();
                    connectorId = clearedChargingLimitRequest.EvseId;
                    Logger.LogInformation("ClearedChargingLimit => Source={0}", source);
                }
                else
                {
                    // Unknown charge station
                    errorCode = ErrorCodes.GenericError;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(clearedChargingLimitResponse);
                Logger.LogTrace("ClearedChargingLimit => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ClearedChargingLimit => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.InternalError;
            }

            WriteMessageLog(ChargePointStatus.Id, connectorId, msgIn.Action, source, errorCode);
            return errorCode;
        }
    }
}
