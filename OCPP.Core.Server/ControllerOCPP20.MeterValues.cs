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
        public string HandleMeterValues(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;
            MeterValuesResponse meterValuesResponse = new MeterValuesResponse();

            meterValuesResponse.CustomData = new CustomDataType();
            meterValuesResponse.CustomData.VendorId = VendorId;

            int connectorId = -1;
            string msgMeterValue = string.Empty;

            try
            {
                Logger.LogTrace("Processing meter values...");
                MeterValuesRequest meterValueRequest = DeserializeMessage<MeterValuesRequest>(msgIn);
                Logger.LogTrace("MeterValues => Message deserialized");

                connectorId = meterValueRequest.EvseId;

                if (ChargePointStatus != null)
                {
                    // Known charge station => extract meter values with correct scale
                    double currentChargeKW = -1;
                    double meterKWH = -1;
                    DateTimeOffset? meterTime = null;
                    double stateOfCharge = -1;
                    GetMeterValues(meterValueRequest.MeterValue, out meterKWH, out currentChargeKW, out stateOfCharge, out meterTime);

                    // If msg contains no time stamp => use current time
                    if (!meterTime.HasValue) meterTime = DateTime.UtcNow;

                    // write charging/meter data in chargepoint status
                    if (connectorId > 0)
                    {
                        msgMeterValue = $"Meter (kWh): {meterKWH}";

                        if (meterKWH >= 0)
                        {
                            UpdateConnectorStatus(connectorId, null, null, meterKWH, meterTime);
                            UpdateMemoryConnectorStatus(connectorId, meterKWH, meterTime.Value, null, null);
                        }
                    }
                }
                else
                {
                    // Unknown charge station
                    errorCode = ErrorCodes.GenericError;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(meterValuesResponse);
                Logger.LogTrace("MeterValues => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "MeterValues => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.InternalError;
            }

            WriteMessageLog(ChargePointStatus.Id, connectorId, msgIn.Action, msgMeterValue, errorCode);
            return errorCode;
        }
    }
}
